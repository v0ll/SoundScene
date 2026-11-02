using SoundScene.Core.Audio;
using SoundScene.Core.Hotkeys;

namespace SoundScene.Core.Settings;

/// <summary>The on-disk shape of settings written before macros had an action.</summary>
/// <remarks>
/// Version 1 only knew toggles, so every macro in such a file has ProfileA and
/// ProfileB and no action. Deserialising that straight into
/// <see cref="HotkeyMacro"/> would silently turn every toggle into an absolute
/// switch to Movie, which is why the version is checked first.
/// </remarks>
internal sealed class LegacySettingsV1
{
    public int Version { get; set; }

    public List<LegacyToggleMacroV1> Macros { get; set; } = [];
}

internal sealed class LegacyToggleMacroV1
{
    public HotkeyGesture? Gesture { get; set; }

    public AudioProfile ProfileA { get; set; }

    public AudioProfile ProfileB { get; set; }

    public bool Enabled { get; set; } = true;

    public HotkeyMacro ToMacro() => new()
    {
        Gesture = Gesture ?? new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x4D),
        Action = MacroAction.ToggleProfiles,
        ProfileA = ProfileA,
        ProfileB = ProfileB,
        Enabled = Enabled,
    };
}
