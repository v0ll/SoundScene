using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using SoundScene.App.Services;
using SoundScene.App.ViewModels;
using SoundScene.App.Views;
using SoundScene.Core.Audio;
using SoundScene.Core.Backends.Dolby;
using SoundScene.Core.Hotkeys;
using SoundScene.Core.Scenes;
using SoundScene.Core.Settings;
using Application = System.Windows.Application;

namespace SoundScene.App;

[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "The WPF Application lifetime is the owner; all fields are disposed in OnExit.")]
public partial class App : Application
{
    private readonly SettingsStore _settingsStore = new();
    private SingleInstance? _instance;
    private DolbyDaxBackend? _backend;
    private SceneLibrary? _scenes;
    private TrayViewModel? _viewModel;
    private PopupWindow? _popup;
    private TrayIconService? _tray;
    private HotkeyService? _hotkeys;
    private AppSettings _settings = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

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

        _settings = _settingsStore.Load();

        _backend = new DolbyDaxBackend();
        _scenes = new SceneLibrary(_backend);
        _scenes.Load();

        _viewModel = new TrayViewModel(_backend, _scenes, PromptForSceneName);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _popup = new PopupWindow { DataContext = _viewModel };
        _popup.EqualizerRequested += OnEqualizerRequested;

        _hotkeys = new HotkeyService();

        _tray = new TrayIconService();
        _tray.PopupRequested += OnPopupRequested;
        _tray.SettingsRequested += OnSettingsRequested;
        _tray.EqualizerRequested += OnEqualizerRequested;
        _tray.DiagnosticsRequested += OnDiagnosticsRequested;
        _tray.ExitRequested += OnExitRequested;

        ApplyHotkeys();
        _ = _viewModel.RefreshAsync();
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

    private async void OnMacroPressed(ToggleMacro macro)
    {
        if (_backend is null || _viewModel is null)
        {
            return;
        }

        try
        {
            var state = await _backend.GetStateAsync().ConfigureAwait(true);
            var target = macro.Next(state.Profile);
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
        var current = _settings.Macros.FirstOrDefault() ?? AppSettings.CreateDefault().Macros[0];

        var window = new MacroWindow(current);
        if (window.ShowDialog() != true || window.Result is null)
        {
            return;
        }

        _settings.Macros = [window.Result];
        _settingsStore.Save(_settings);
        ApplyHotkeys();

        _viewModel?.SetStatus($"Hotkey set to {window.Result.Gesture.DisplayName}");
    }

    private string? PromptForSceneName(string suggested)
    {
        var window = new PromptWindow("Save scene", "Name this scene", suggested);

        if (_popup is not null)
        {
            window.Owner = _popup;
        }

        return window.ShowDialog() == true ? window.Result : null;
    }

    private async void OnPopupRequested(object? sender, EventArgs e)
    {
        if (_popup is null || _viewModel is null)
        {
            return;
        }

        _popup.ShowNearTray();
        await _viewModel.RefreshAsync().ConfigureAwait(true);
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

        var window = new DiagnosticsWindow(_backend);
        window.ShowDialog();
    }

    private void OnExitRequested(object? sender, EventArgs e) => Shutdown();

    protected override void OnExit(ExitEventArgs e)
    {
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
