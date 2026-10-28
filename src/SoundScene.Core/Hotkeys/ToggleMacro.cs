using SoundScene.Core.Audio;

namespace SoundScene.Core.Hotkeys;

/// <summary>
/// A single global hotkey that flips between two profiles.
/// </summary>
/// <remarks>
/// This is deliberately not a general macro engine. The whole point of the app
/// is "one key, A or B", and a toggle needs no stored state: it reads the
/// current profile and moves to the other one. That also means it stays correct
/// when the profile is changed from Dolby Access itself.
/// </remarks>
public sealed record ToggleMacro
{
    public required HotkeyGesture Gesture { get; init; }

    public required AudioProfile ProfileA { get; init; }

    public required AudioProfile ProfileB { get; init; }

    public bool Enabled { get; init; } = true;

    /// <summary>Given the profile that is active now, which one should be next.</summary>
    public AudioProfile Next(AudioProfile current) =>
        current == ProfileA ? ProfileB : ProfileA;

    public string Description =>
        $"{Gesture.DisplayName}  \u00B7  {ProfileA.DisplayName()} \u2194 {ProfileB.DisplayName()}";
}
