namespace SoundScene.Core.Hotkeys;

/// <summary>
/// A key combination, stored as Win32 virtual-key code plus modifier flags so it
/// can be handed straight to <c>RegisterHotKey</c> and persisted as JSON.
/// </summary>
public sealed record HotkeyGesture
{
    public HotkeyGesture(HotkeyModifiers modifiers, uint virtualKey)
    {
        if (virtualKey == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(virtualKey), "A hotkey needs a key.");
        }

        Modifiers = modifiers;
        VirtualKey = virtualKey;
    }

    public HotkeyModifiers Modifiers { get; }

    public uint VirtualKey { get; }

    /// <summary>Human-readable form, e.g. <c>Ctrl+Alt+M</c>.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string DisplayName
    {
        get
        {
            var parts = new List<string>(5);

            if (Modifiers.HasFlag(HotkeyModifiers.Control))
            {
                parts.Add("Ctrl");
            }

            if (Modifiers.HasFlag(HotkeyModifiers.Alt))
            {
                parts.Add("Alt");
            }

            if (Modifiers.HasFlag(HotkeyModifiers.Shift))
            {
                parts.Add("Shift");
            }

            if (Modifiers.HasFlag(HotkeyModifiers.Win))
            {
                parts.Add("Win");
            }

            parts.Add(KeyName(VirtualKey));
            return string.Join('+', parts);
        }
    }

    /// <summary>
    /// Names a virtual-key code. Covers the keys a person would actually bind;
    /// anything else falls back to a hex code rather than throwing.
    /// </summary>
    public static string KeyName(uint virtualKey) => virtualKey switch
    {
        >= 0x41 and <= 0x5A => ((char)virtualKey).ToString(),
        >= 0x30 and <= 0x39 => ((char)virtualKey).ToString(),
        >= 0x60 and <= 0x69 => $"Num{virtualKey - 0x60}",
        >= 0x70 and <= 0x87 => $"F{virtualKey - 0x6F}",
        0x08 => "Backspace",
        0x09 => "Tab",
        0x0D => "Enter",
        0x13 => "Pause",
        0x14 => "CapsLock",
        0x1B => "Esc",
        0x20 => "Space",
        0x21 => "PageUp",
        0x22 => "PageDown",
        0x23 => "End",
        0x24 => "Home",
        0x25 => "Left",
        0x26 => "Up",
        0x27 => "Right",
        0x28 => "Down",
        0x2C => "PrintScreen",
        0x2D => "Insert",
        0x2E => "Delete",
        0xBA => ";",
        0xBB => "=",
        0xBC => ",",
        0xBD => "-",
        0xBE => ".",
        0xBF => "/",
        0xC0 => "`",
        0xDB => "[",
        0xDC => "\\",
        0xDD => "]",
        0xDE => "'",
        _ => $"0x{virtualKey:X2}",
    };
}
