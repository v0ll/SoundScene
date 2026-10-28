namespace SoundScene.Core.Hotkeys;

/// <summary>Modifier flags, ordered to match the Win32 MOD_* constants.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8,
}
