using System.Windows.Input;
using SoundScene.Core.Audio;

namespace SoundScene.App.ViewModels;

/// <summary>One tile in the profile grid.</summary>
public sealed class ProfileItemViewModel : Mvvm.ObservableObject
{
    private bool _isActive;

    public ProfileItemViewModel(AudioProfile profile, Func<AudioProfile, Task> select, Action<Exception> onError)
    {
        Profile = profile;
        Name = profile.DisplayName();
        Glyph = profile.Glyph();
        Command = new Mvvm.AsyncRelayCommand(() => select(profile), onError);
    }

    public AudioProfile Profile { get; }

    public string Name { get; }

    public string Glyph { get; }

    public ICommand Command { get; }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }
}
