using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Windows;
using SoundScene.App.Services;
using SoundScene.App.ViewModels;
using SoundScene.App.Views;
using SoundScene.Core.Audio;
using SoundScene.Core.Backends.Dolby;
using SoundScene.Core.Hotkeys;
using SoundScene.Core.Scenes;
using SoundScene.Core.Settings;
using SoundScene.Core.Storage;
using Application = System.Windows.Application;

namespace SoundScene.App;

[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "The WPF Application lifetime is the owner; all fields are disposed in OnExit.")]
public partial class App : Application
{
    private const string PortableArgument = "--portable";
    private const string PortableMarker = "portable.txt";
    private const string ResetDolbyArgument = "--reset-dolby";

    private SettingsStore? _settingsStore;
    private SingleInstance? _instance;
    private DolbyDaxBackend? _backend;
    private SceneLibrary? _scenes;
    private TrayViewModel? _viewModel;
    private PopupWindow? _popup;
    private TrayIconService? _tray;
    private HotkeyService? _hotkeys;
    private IdleMemoryTrimmer? _trimmer;
    private AppSettings _settings = new();
    private bool _setupPrompted;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Portable copies keep everything beside the executable, so this has to
        // happen before any store is constructed.
        if (e.Args.Contains(PortableArgument, StringComparer.OrdinalIgnoreCase)
            || File.Exists(Path.Combine(AppContext.BaseDirectory, PortableMarker)))
        {
            AppPaths.UsePortable(AppContext.BaseDirectory);
        }

        // Uninstall uses this to put Dolby back to a neutral state before the
        // app's files disappear. Best effort: a missing Dolby install must not
        // keep the uninstaller from finishing.
        if (e.Args.Contains(ResetDolbyArgument, StringComparer.OrdinalIgnoreCase))
        {
            _ = ResetDolbyAsync();
            return;
        }

        // Two instances would fight over the same DAX profile, so the second
        // launch just asks the running one for its popup and leaves.
        _instance = SingleInstance.TryAcquire();
        if (_instance is null)
        {
            SingleInstance.SignalRunningInstance();
            Shutdown();
            return;
        }

        _instance.ActivationRequested += OnActivationRequested;

        _settingsStore = new SettingsStore();
        _settings = _settingsStore.Load();

        _backend = new DolbyDaxBackend();
        _scenes = new SceneLibrary(_backend);
        _scenes.Load();

        _viewModel = new TrayViewModel(_backend, _scenes, PromptForSceneName);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.ApplyRequirements(DaxRequirementChecker.Check());

        // The popup is built on first use. Starting with Windows should cost a
        // tray icon and a view model, not a hidden WPF window tree.
        _trimmer = new IdleMemoryTrimmer();
        _hotkeys = new HotkeyService();

        _tray = new TrayIconService(allowStartup: !AppPaths.IsPortable);
        _tray.PopupRequested += OnPopupRequested;
        _tray.SettingsRequested += OnSettingsRequested;
        _tray.EqualizerRequested += OnEqualizerRequested;
        _tray.DiagnosticsRequested += OnDiagnosticsRequested;
        _tray.SetupRequested += OnSetupRequested;
        _tray.ExitRequested += OnExitRequested;
        _tray.SetStatus(_viewModel.Status);

        ApplyHotkeys();
        _ = _viewModel.RefreshAsync();

        // Launching the app by hand should show something; starting with Windows
        // passes --background so the login stays quiet and only the tray appears.
        if (!e.Args.Contains(StartupRegistration.BackgroundArgument, StringComparer.OrdinalIgnoreCase))
        {
            _ = Dispatcher.BeginInvoke(() => OnPopupRequested(this, EventArgs.Empty));
        }
    }

    /// <summary>Puts Personalize back to flat/off, then exits without showing UI.</summary>
    private async Task ResetDolbyAsync()
    {
        try
        {
            await using var backend = new DolbyDaxBackend();
            var state = await backend.GetStateAsync().ConfigureAwait(true);
            await backend.SetProfileAsync(AudioProfile.Personalize).ConfigureAwait(true);
            await backend.SetGeqAsync(GraphicEqualizer.Flat()).ConfigureAwait(true);
            await backend.SetVolumeLevelerAsync(false).ConfigureAwait(true);
            await backend.SetSurroundVirtualizerAsync(false).ConfigureAwait(true);
            await backend.SetProfileAsync(state.Profile).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is AudioBackendException or InvalidOperationException)
        {
            // No Dolby installation to reset. Uninstall continues regardless.
        }

        Shutdown(0);
    }

    // Keeps the tray tooltip on the same status line the popup shows.
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TrayViewModel.Status) && _viewModel is not null)
        {
            _tray?.SetStatus(_viewModel.Status);
        }
    }

    private void ApplyHotkeys()
    {
        if (_hotkeys is null || _viewModel is null)
        {
            return;
        }

        _hotkeys.UnregisterAll();

        var failures = new List<string>();

        foreach (var macro in _settings.Macros.Where(m => m.Enabled))
        {
            if (!_hotkeys.TryRegister(macro.Gesture, () => OnMacroPressed(macro), out var error))
            {
                failures.Add($"{macro.Gesture.DisplayName}: {error}");
            }
        }

        if (failures.Count > 0)
        {
            _viewModel.SetStatus("Hotkey unavailable \u2014 " + string.Join("; ", failures));
        }
    }

    private async void OnMacroPressed(HotkeyMacro macro)
    {
        if (_backend is null || _viewModel is null)
        {
            return;
        }

        _trimmer?.Touch();

        try
        {
            // An absolute action needs no read; a toggle has to see what is
            // active right now, including changes made in Dolby Access.
            var target = macro.Profile;

            if (macro.Action == MacroAction.ToggleProfiles)
            {
                var state = await _backend.GetStateAsync().ConfigureAwait(true);
                target = macro.Next(state.Profile);
            }

            await _backend.SetProfileAsync(target).ConfigureAwait(true);
            await _viewModel.RefreshAsync().ConfigureAwait(true);
        }
        catch (AudioBackendException ex)
        {
            _viewModel.SetStatus(ex.Message);
        }
    }

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        _trimmer?.Touch();

        var window = new MacroWindow(_settings.Macros);
        if (window.ShowDialog() != true)
        {
            return;
        }

        _settings.Macros = [.. window.Result];
        _settingsStore?.Save(_settings);
        ApplyHotkeys();

        _viewModel?.SetStatus(window.Result.Count switch
        {
            0 => "No hotkeys are set",
            1 => $"1 hotkey \u00B7 {window.Result[0].Description}",
            var count => $"{count} hotkeys active",
        });
    }

    private string? PromptForSceneName(string suggested)
    {
        var window = new PromptWindow("Save scene", "Name this scene", suggested);

        if (_popup is { IsVisible: true })
        {
            window.Owner = _popup;
        }

        return window.ShowDialog() == true ? window.Result : null;
    }

    private PopupWindow EnsurePopup()
    {
        if (_popup is null)
        {
            _popup = new PopupWindow { DataContext = _viewModel };
            _popup.EqualizerRequested += OnEqualizerRequested;
            _popup.HotkeysRequested += OnSettingsRequested;
            _popup.SetupRequested += OnSetupRequested;
        }

        return _popup;
    }

    private async void OnPopupRequested(object? sender, EventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        _trimmer?.Touch();
        EnsurePopup().ShowNearTray();
        await _viewModel.RefreshAsync().ConfigureAwait(true);

        // Show the setup window once per session on the first popup, then let
        // the in-popup banner carry the message.
        if (_viewModel.NeedsSetup && !_setupPrompted)
        {
            _setupPrompted = true;
            ShowRequirements();
        }
    }

    // Another launch asked us to surface the popup. Comes from a pool thread.
    private void OnActivationRequested(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(() => OnPopupRequested(this, EventArgs.Empty));

    private void OnEqualizerRequested(object? sender, EventArgs e)
    {
        if (_backend is null)
        {
            return;
        }

        if (_viewModel is { IsAvailable: false })
        {
            ShowRequirements();
            return;
        }

        _trimmer?.Touch();

        var window = new EqWindow(new EqViewModel(_backend));

        if (_popup is { IsVisible: true })
        {
            window.Owner = _popup;
        }

        window.ShowDialog();

        // Applying switches to Personalize, so the popup's active tile moved.
        _ = _viewModel?.RefreshAsync();
    }

    private void OnDiagnosticsRequested(object? sender, EventArgs e)
    {
        if (_backend is null)
        {
            return;
        }
        _trimmer?.Touch();

        var window = new DiagnosticsWindow(_backend);
        window.ShowDialog();
    }

    private void OnSetupRequested(object? sender, EventArgs e) => ShowRequirements();

    /// <summary>Shows the prerequisite checklist and re-opens DAX if it passes.</summary>
    private void ShowRequirements()
    {
        _setupPrompted = true;

        var window = new RequirementsWindow(DaxRequirementChecker.Check());

        if (_popup is { IsVisible: true })
        {
            window.Owner = _popup;
        }

        window.ShowDialog();

        var updated = DaxRequirementChecker.Check();
        _viewModel?.ApplyRequirements(updated);

        if (updated.IsReady)
        {
            _ = _viewModel?.RefreshAsync();
        }
    }

    private void OnExitRequested(object? sender, EventArgs e) => Shutdown();

    protected override void OnExit(ExitEventArgs e)
    {
        _trimmer?.Dispose();
        _tray?.Dispose();
        _hotkeys?.Dispose();
        _instance?.Dispose();

        if (_backend is not null)
        {
            _backend.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        base.OnExit(e);
    }
}
