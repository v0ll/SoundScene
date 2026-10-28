using System.Windows;
using System.Windows.Input;
using SoundScene.Core.Audio;
using SoundScene.Core.Hotkeys;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace SoundScene.App.Views;

/// <summary>Lets the user record a hotkey and pick the two profiles it toggles.</summary>
public partial class MacroWindow : Window
{
    private HotkeyModifiers _modifiers;
    private uint _virtualKey;

    public MacroWindow(ToggleMacro macro)
    {
        ArgumentNullException.ThrowIfNull(macro);

        InitializeComponent();

        var profiles = Enum.GetValues<AudioProfile>();
        ProfileACombo.ItemsSource = profiles;
        ProfileBCombo.ItemsSource = profiles;
        ProfileACombo.SelectedItem = macro.ProfileA;
        ProfileBCombo.SelectedItem = macro.ProfileB;

        _modifiers = macro.Gesture.Modifiers;
        _virtualKey = macro.Gesture.VirtualKey;

        UpdateGestureText();
        UpdatePreview();
    }

    /// <summary>Set when the user saves.</summary>
    public ToggleMacro? Result { get; private set; }

    private void OnCaptureClick(object sender, MouseButtonEventArgs e) => CaptureBox.Focus();

    private void OnCaptureKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = ReadModifiers();

        if (IsModifierKey(key))
        {
            GestureText.Text = Describe(modifiers, 0) + "\u2026";
            return;
        }

        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (virtualKey == 0)
        {
            return;
        }

        _modifiers = modifiers;
        _virtualKey = virtualKey;

        HintText.Text = modifiers == HotkeyModifiers.None
            ? "Add Ctrl, Alt, Shift or Win - an unmodified key would swallow normal typing."
            : string.Empty;

        UpdateGestureText();
        UpdatePreview();
    }

    private void OnProfileChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        UpdatePreview();

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (_virtualKey == 0)
        {
            HintText.Text = "Record a key combination first.";
            return;
        }

        if (_modifiers == HotkeyModifiers.None)
        {
            HintText.Text = "The hotkey needs at least one modifier.";
            return;
        }

        if (ProfileACombo.SelectedItem is not AudioProfile profileA ||
            ProfileBCombo.SelectedItem is not AudioProfile profileB)
        {
            HintText.Text = "Pick both profiles.";
            return;
        }

        if (profileA == profileB)
        {
            HintText.Text = "Pick two different profiles, otherwise the toggle does nothing.";
            return;
        }

        Result = new ToggleMacro
        {
            Gesture = new HotkeyGesture(_modifiers, _virtualKey),
            ProfileA = profileA,
            ProfileB = profileB,
        };

        DialogResult = true;
    }

    private void UpdateGestureText()
    {
        GestureText.Text = _virtualKey == 0
            ? "Click here, then press keys"
            : Describe(_modifiers, _virtualKey);
    }

    private void UpdatePreview()
    {
        if (ProfileACombo.SelectedItem is not AudioProfile profileA ||
            ProfileBCombo.SelectedItem is not AudioProfile profileB)
        {
            PreviewText.Text = string.Empty;
            return;
        }

        var gesture = _virtualKey == 0 ? "the hotkey" : Describe(_modifiers, _virtualKey);
        PreviewText.Text = $"{gesture} switches between {profileA.DisplayName()} and {profileB.DisplayName()}.";
    }

    private static string Describe(HotkeyModifiers modifiers, uint virtualKey)
    {
        var parts = new List<string>(5);

        if (modifiers.HasFlag(HotkeyModifiers.Control))
        {
            parts.Add("Ctrl");
        }

        if (modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            parts.Add("Alt");
        }

        if (modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            parts.Add("Shift");
        }

        if (modifiers.HasFlag(HotkeyModifiers.Win))
        {
            parts.Add("Win");
        }

        if (virtualKey != 0)
        {
            parts.Add(HotkeyGesture.KeyName(virtualKey));
        }

        return parts.Count == 0 ? string.Empty : string.Join('+', parts);
    }

    private static HotkeyModifiers ReadModifiers()
    {
        var modifiers = HotkeyModifiers.None;

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            modifiers |= HotkeyModifiers.Control;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            modifiers |= HotkeyModifiers.Alt;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            modifiers |= HotkeyModifiers.Shift;
        }

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows))
        {
            modifiers |= HotkeyModifiers.Win;
        }

        return modifiers;
    }

    private static bool IsModifierKey(Key key) => key
        is Key.LeftCtrl or Key.RightCtrl
        or Key.LeftAlt or Key.RightAlt
        or Key.LeftShift or Key.RightShift
        or Key.LWin or Key.RWin;
}
