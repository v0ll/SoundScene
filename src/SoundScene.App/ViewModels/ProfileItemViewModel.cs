using System.Windows.Input;
using SoundScene.Core.Audio;

namespace SoundScene.App.ViewModels;

/// <summary>One tile in the profile grid.</summary>
public sealed class ProfileItemViewModel : Mvvm.ObservableObject
{
    private bool _isActive;

    public ProfileItemViewModel(
        AudioProfileDescriptor descriptor,
        Func<int, Task> select,
        Action<Exception> onError)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        Descriptor = descriptor;
        Command = new Mvvm.AsyncRelayCommand(() => select(descriptor.Index), onError);
    }

    public AudioProfileDescriptor Descriptor { get; }

    /// <summary>The driver's real index, which is what selection uses.</summary>
    public int ProfileIndex => Descriptor.Index;

    /// <summary>Null when the driver exposed a mode SoundScene does not know.</summary>
    public AudioProfile? Profile => Descriptor.KnownProfile;

    public string Name => Descriptor.DisplayName;

    public string Glyph => Descriptor.Glyph;

    public ICommand Command { get; }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }
}
