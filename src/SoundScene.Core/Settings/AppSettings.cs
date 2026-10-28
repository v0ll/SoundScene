using SoundScene.Core.Audio;
using SoundScene.Core.Hotkeys;

namespace SoundScene.Core.Settings;

/// <summary>Everything the app persists. Versioned so old files stay loadable.</summary>
public sealed class AppSettings
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public List<ToggleMacro> Macros { get; set; } = [];

    /// <summary>
    /// A sensible starting point: one hotkey, Movie against Game. Used only when
    /// there is no settings file yet, so the app is useful on first launch.
    /// </summary>
    public static AppSettings CreateDefault() => new()
    {
        Macros =
        [
            new ToggleMacro
            {
                Gesture = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x4D),
                ProfileA = AudioProfile.Movie,
                ProfileB = AudioProfile.Game,
            },
        ],
    };
}
