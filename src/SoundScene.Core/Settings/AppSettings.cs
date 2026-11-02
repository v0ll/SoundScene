using SoundScene.Core.Audio;
using SoundScene.Core.Hotkeys;

namespace SoundScene.Core.Settings;

/// <summary>Everything the app persists. Versioned so old files stay loadable.</summary>
public sealed class AppSettings
{
    /// <summary>
    /// 1: a single A/B toggle per macro.
    /// 2: macros carry an action, so one hotkey can switch straight to a profile.
    /// </summary>
    public const int CurrentVersion = 2;

    public int Version { get; set; } = CurrentVersion;

    public List<HotkeyMacro> Macros { get; set; } = [];

    /// <summary>
    /// A sensible starting point: Ctrl+Alt+M still toggles Movie against Game,
    /// which is what the app has always done. Used only when there is no
    /// settings file yet.
    /// </summary>
    public static AppSettings CreateDefault() => new()
    {
        Macros =
        [
            new HotkeyMacro
            {
                Gesture = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x4D),
                Action = MacroAction.ToggleProfiles,
                ProfileA = AudioProfile.Movie,
                ProfileB = AudioProfile.Game,
            },
        ],
    };
}
