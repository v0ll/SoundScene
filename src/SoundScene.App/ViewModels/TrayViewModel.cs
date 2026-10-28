using System.Collections.ObjectModel;
using SoundScene.App.Mvvm;
using SoundScene.Core.Audio;

namespace SoundScene.App.ViewModels;

/// <summary>Backs the tray popup: the profile grid plus a status line.</summary>
public sealed class TrayViewModel : ObservableObject
{
    private readonly IAudioBackend _backend;
    private string _status = "Starting\u2026";
    private bool _isAvailable = true;

    public TrayViewModel(IAudioBackend backend)
    {
        _backend = backend;
        Profiles = new ObservableCollection<ProfileItemViewModel>(
            Enum.GetValues<AudioProfile>()
                .Select(profile => new ProfileItemViewModel(profile, SelectProfileAsync, ReportError)));
    }

    public ObservableCollection<ProfileItemViewModel> Profiles { get; }

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

    public async Task RefreshAsync()
    {
        try
        {
            var state = await _backend.GetStateAsync().ConfigureAwait(true);

            foreach (var item in Profiles)
            {
                item.IsActive = item.Profile == state.Profile;
            }

            IsAvailable = true;
            Status = state.DolbyEnabled
                ? $"Dolby on \u00B7 {state.Profile.DisplayName()}"
                : "Dolby processing is off";
        }
        catch (Exception ex)
        {
            ReportError(ex);
        }
    }

    private async Task SelectProfileAsync(AudioProfile profile)
    {
        await _backend.SetProfileAsync(profile).ConfigureAwait(true);
        await RefreshAsync().ConfigureAwait(true);
    }

    private void ReportError(Exception exception)
    {
        IsAvailable = false;
        Status = exception.Message;
    }
}
