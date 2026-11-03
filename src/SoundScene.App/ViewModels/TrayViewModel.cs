using System.Collections.ObjectModel;
using System.Windows.Input;
using SoundScene.App.Mvvm;
using SoundScene.Core.Audio;
using SoundScene.Core.Backends.Dolby;
using SoundScene.Core.Scenes;

namespace SoundScene.App.ViewModels;

/// <summary>Backs the tray popup: the profile grid, the scene list, and a status line.</summary>
public sealed class TrayViewModel : ObservableObject
{
    private readonly IAudioBackend _backend;
    private readonly SceneLibrary _scenes;
    private readonly Func<string, string?>? _askForSceneName;
    private string _status = "Starting\u2026";
    private bool _isAvailable = true;
    private bool _needsSetup;
    private string _requirementSummary = string.Empty;
    private int _currentProfile;
    private AudioCapabilities _capabilities = AudioCapabilities.Unknown;

    public TrayViewModel(
        IAudioBackend backend,
        SceneLibrary scenes,
        Func<string, string?>? askForSceneName = null)
    {
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(scenes);

        _backend = backend;
        _scenes = scenes;
        _askForSceneName = askForSceneName;

        Profiles = [];
        BuildProfiles(AudioProfileCatalog.DefaultProfiles);

        CaptureSceneCommand = new AsyncRelayCommand(CaptureSceneAsync, ReportError);
        ReloadScenes();
    }

    public ObservableCollection<ProfileItemViewModel> Profiles { get; } = [];

    public ObservableCollection<SceneItemViewModel> Scenes { get; } = [];

    public ICommand CaptureSceneCommand { get; }

    public string BackendName => _backend.Name;

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public bool IsAvailable
    {
        get => _isAvailable;
        private set => SetProperty(ref _isAvailable, value);
    }

    /// <summary>True when a prerequisite is missing, so the setup banner is shown.</summary>
    public bool NeedsSetup
    {
        get => _needsSetup;
        private set => SetProperty(ref _needsSetup, value);
    }

    public string RequirementSummary
    {
        get => _requirementSummary;
        private set => SetProperty(ref _requirementSummary, value);
    }

    /// <summary>Applies the result of a requirements check to the popup and tray.</summary>
    public void ApplyRequirements(DaxRequirements requirements)
    {
        ArgumentNullException.ThrowIfNull(requirements);

        NeedsSetup = !requirements.IsReady;
        RequirementSummary = requirements.Summary;

        if (NeedsSetup)
        {
            Status = "Setup required \u00B7 " + RequirementSummary;
        }
    }

    /// <summary>Overrides the status line, e.g. for hotkey registration failures.</summary>
    public void SetStatus(string text) => Status = text;

    /// <summary>Selects a profile by grid position, for the popup's number keys.</summary>
    public void SelectProfileAt(int index)
    {
        if (index < 0 || index >= Profiles.Count)
        {
            return;
        }

        var command = Profiles[index].Command;
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    public async Task RefreshAsync()
    {
        try
        {
            var capabilities = await _backend.GetCapabilitiesAsync().ConfigureAwait(true);
            ApplyCapabilities(capabilities);

            var state = await _backend.GetStateAsync().ConfigureAwait(true);

            _currentProfile = state.ProfileIndex;

            foreach (var item in Profiles)
            {
                item.IsActive = item.ProfileIndex == state.ProfileIndex;
            }

            IsAvailable = true;
            NeedsSetup = false;
            Status = state.DolbyEnabled
                ? $"Dolby on \u00B7 {state.ProfileName ?? state.Profile.DisplayName()}"
                : "Dolby processing is off";
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    private void ApplyCapabilities(AudioCapabilities capabilities)
    {
        _capabilities = capabilities;

        if (!Profiles.Select(item => item.Descriptor).SequenceEqual(capabilities.Profiles))
        {
            BuildProfiles(capabilities.Profiles);
        }
    }

    private void BuildProfiles(IReadOnlyList<AudioProfileDescriptor> descriptors)
    {
        Profiles.Clear();

        foreach (var descriptor in descriptors)
        {
            Profiles.Add(new ProfileItemViewModel(descriptor, SelectProfileAsync, ReportError));
        }
    }

    /// <summary>Rebuilds the scene rows from the library, preserving nothing but order.</summary>
    public void ReloadScenes()
    {
        Scenes.Clear();

        foreach (var scene in _scenes.Scenes)
        {
            Scenes.Add(new SceneItemViewModel(scene, ApplySceneAsync, DeleteScene, ReportError));
        }
    }

    private async Task SelectProfileAsync(int index)
    {
        await _backend.SetProfileIndexAsync(index).ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
    }

    private async Task CaptureSceneAsync()
    {
        var suggested = _capabilities.ProfileAt(_currentProfile)?.DisplayName
                        ?? (_capabilities.Profiles.Count > 0 ? _capabilities.Profiles[0].DisplayName : null)
                        ?? "Profile";
        var name = _askForSceneName is null ? suggested : _askForSceneName(suggested);

        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var scene = await _scenes.CaptureAsync(name).ConfigureAwait(true);

        ReloadScenes();
        IsAvailable = true;
        Status = $"Saved scene \u201C{scene.Name}\u201D";
    }

    private async Task ApplySceneAsync(Scene scene)
    {
        await _scenes.ApplyAsync(scene).ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
        Status = $"Applied \u201C{scene.Name}\u201D \u00B7 {scene.Summary}";
    }

    private void DeleteScene(Scene scene)
    {
        if (!_scenes.Remove(scene.Name))
        {
            return;
        }

        ReloadScenes();
        Status = $"Removed \u201C{scene.Name}\u201D";
    }

    private void ReportError(Exception exception)
    {
        IsAvailable = false;
        Status = NeedsSetup
            ? "Setup required \u00B7 " + RequirementSummary
            : exception.Message;
    }
}
