using SoundScene.Core.Audio;

namespace SoundScene.Core.Hotkeys;

/// <summary>What a hotkey does when it fires.</summary>
public enum MacroAction
{
    /// <summary>Jump straight to one profile.</summary>
    ActivateProfile,

    /// <summary>Flip between two profiles, based on whichever is active.</summary>
    ToggleProfiles,
}

/// <summary>
/// One global hotkey and the profile change it performs.
/// </summary>
/// <remarks>
/// A toggle needs no stored state: it reads the current profile and moves to the
/// other one, so it stays correct when the profile is changed from Dolby Access
/// itself. An absolute action is the opposite trade-off - it is predictable and
/// is what "one key per mode" needs.
/// </remarks>
public sealed record HotkeyMacro
{
    public required HotkeyGesture Gesture { get; init; }

    public MacroAction Action { get; init; } = MacroAction.ActivateProfile;

    /// <summary>Target of <see cref="MacroAction.ActivateProfile"/>.</summary>
    public AudioProfile Profile { get; init; } = AudioProfile.Movie;

    /// <summary>First profile of <see cref="MacroAction.ToggleProfiles"/>.</summary>
    public AudioProfile ProfileA { get; init; } = AudioProfile.Movie;

    /// <summary>Second profile of <see cref="MacroAction.ToggleProfiles"/>.</summary>
    public AudioProfile ProfileB { get; init; } = AudioProfile.Game;

    public bool Enabled { get; init; } = true;

    /// <summary>Given the profile that is active now, which one should be next.</summary>
    public AudioProfile Next(AudioProfile current) => Action == MacroAction.ToggleProfiles
        ? (current == ProfileA ? ProfileB : ProfileA)
        : Profile;

    [System.Text.Json.Serialization.JsonIgnore]
    public string Description => Action == MacroAction.ToggleProfiles
        ? $"{Gesture.DisplayName}  \u00B7  {ProfileA.DisplayName()} \u2194 {ProfileB.DisplayName()}"
        : $"{Gesture.DisplayName}  \u00B7  \u2192 {Profile.DisplayName()}";
}
