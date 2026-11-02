using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using SoundScene.App.Services;
using SoundScene.App.ViewModels;
using SoundScene.Core.Audio;
using SoundScene.Core.Hotkeys;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace SoundScene.App.Views;

/// <summary>
/// Edits the whole hotkey list: one key per profile, or toggles between two.
/// </summary>
/// <remarks>
/// Everything is edited in place in the list - there is no OK/Apply per row -
/// so the list is the source of truth and Save only validates it.
/// </remarks>
public partial class MacroWindow : Window
{
    private readonly ObservableCollection<MacroRowViewModel> _rows = [];
    private HotkeyModifiers _pendingModifiers;
    private uint _pendingVirtualKey;
    private bool _loading;

    public MacroWindow(IReadOnlyList<HotkeyMacro> macros)
    {
        ArgumentNullException.ThrowIfNull(macros);

        InitializeComponent();
        DarkTitleBar.Apply(this);

        ActionCombo.ItemsSource = new[]
        {
            new ActionOption(MacroAction.ActivateProfile, "Switch to a profile"),
            new ActionOption(MacroAction.ToggleProfiles, "Toggle between two profiles"),
        };

        var profiles = Enum.GetValues<AudioProfile>()
            .Select(profile => new ProfileOption(profile, profile.DisplayName()))
            .ToArray();

        ProfileCombo.ItemsSource = profiles;
        ProfileACombo.ItemsSource = profiles;
        ProfileBCombo.ItemsSource = profiles;

        foreach (var macro in macros)
        {
            _rows.Add(new MacroRowViewModel(macro));
        }

        MacroList.ItemsSource = _rows;
        MacroList.SelectedIndex = _rows.Count > 0 ? 0 : -1;
        UpdateButtons();
    }

    /// <summary>Set when the user saves. Empty means "no hotkeys".</summary>
    public IReadOnlyList<HotkeyMacro> Result { get; private set; } = [];

    private MacroRowViewModel? Selected => MacroList.SelectedItem as MacroRowViewModel;

    private void OnAdd(object sender, RoutedEventArgs e)
    {
        var row = new MacroRowViewModel(CreateMacro());
        _rows.Add(row);
        MacroList.SelectedItem = row;
        UpdateButtons();
        ApplyEditor();
    }

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } row)
        {
            return;
        }

        var index = _rows.IndexOf(row);
        _rows.Remove(row);
        MacroList.SelectedIndex = _rows.Count == 0 ? -1 : Math.Min(index, _rows.Count - 1);
        UpdateButtons();
    }

    private void OnSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        LoadEditor();
    }

    private void OnActionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdatePanelVisibility();
        ApplyEditor();
    }

    private void OnEditorChanged(object sender, RoutedEventArgs e) => ApplyEditor();

    private void OnEditorChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        ApplyEditor();

    private void OnCaptureClick(object sender, MouseButtonEventArgs e) => CaptureBox.Focus();

    private void OnCaptureKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        if (Selected is null)
        {
            return;
        }

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

        _pendingModifiers = modifiers;
        _pendingVirtualKey = virtualKey;

        HintText.Text = modifiers == HotkeyModifiers.None
            ? "Add Ctrl, Alt, Shift or Win - an unmodified key would swallow normal typing."
            : string.Empty;

        GestureText.Text = Describe(modifiers, virtualKey);
        ApplyEditor();
    }

    private void LoadEditor()
    {
        _loading = true;
        try
        {
            var row = Selected;
            EditorPanel.IsEnabled = row is not null;
            UpdateButtons();

            if (row is null)
            {
                GestureText.Text = "No hotkey selected";
                PreviewText.Text = string.Empty;
                return;
            }

            var macro = row.Macro;
            _pendingModifiers = macro.Gesture.Modifiers;
            _pendingVirtualKey = macro.Gesture.VirtualKey;

            GestureText.Text = macro.Gesture.DisplayName;
            ActionCombo.SelectedValue = macro.Action;
            ProfileCombo.SelectedValue = macro.Profile;
            ProfileACombo.SelectedValue = macro.ProfileA;
            ProfileBCombo.SelectedValue = macro.ProfileB;
            EnabledCheck.IsChecked = macro.Enabled;
            HintText.Text = string.Empty;

            UpdatePanelVisibility();
            UpdatePreview();
        }
        finally
        {
            _loading = false;
        }
    }

    private void ApplyEditor()
    {
        if (_loading || Selected is not { } row || _pendingVirtualKey == 0)
        {
            return;
        }

        row.Update(new HotkeyMacro
        {
            Gesture = new HotkeyGesture(_pendingModifiers, _pendingVirtualKey),
            Action = ActionCombo.SelectedValue is MacroAction action ? action : MacroAction.ActivateProfile,
            Profile = ProfileCombo.SelectedValue is AudioProfile profile ? profile : AudioProfile.Movie,
            ProfileA = ProfileACombo.SelectedValue is AudioProfile a ? a : AudioProfile.Movie,
            ProfileB = ProfileBCombo.SelectedValue is AudioProfile b ? b : AudioProfile.Game,
            Enabled = EnabledCheck.IsChecked == true,
        });

        UpdatePreview();
    }

    private void UpdatePanelVisibility()
    {
        var toggle = ActionCombo.SelectedValue is MacroAction.ToggleProfiles;
        SingleProfilePanel.Visibility = toggle ? Visibility.Collapsed : Visibility.Visible;
        ToggleProfilePanel.Visibility = toggle ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdatePreview()
    {
        if (Selected is not { } row)
        {
            PreviewText.Text = string.Empty;
            return;
        }

        var macro = row.Macro;
        PreviewText.Text = macro.Action == MacroAction.ToggleProfiles
            ? $"{macro.Gesture.DisplayName} flips between {macro.ProfileA.DisplayName()} and {macro.ProfileB.DisplayName()}, based on what is active now."
            : $"{macro.Gesture.DisplayName} switches straight to {macro.Profile.DisplayName()}.";
    }

    private void UpdateButtons() => RemoveButton.IsEnabled = Selected is not null;

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var macros = _rows.Select(row => row.Macro).ToArray();

        foreach (var macro in macros)
        {
            if (macro.Gesture.Modifiers == HotkeyModifiers.None)
            {
                Report($"{macro.Gesture.DisplayName}: add Ctrl, Alt, Shift or Win.", macro);
                return;
            }

            if (macro.Action == MacroAction.ToggleProfiles && macro.ProfileA == macro.ProfileB)
            {
                Report($"{macro.Gesture.DisplayName}: pick two different profiles, otherwise the toggle does nothing.", macro);
                return;
            }
        }

        var duplicate = macros
            .GroupBy(macro => macro.Gesture)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicate is not null)
        {
            Report($"{duplicate.Key.DisplayName} is used by more than one hotkey.", duplicate.First());
            return;
        }

        Result = macros;
        DialogResult = true;
    }

    private void Report(string message, HotkeyMacro macro)
    {
        StatusText.Text = message;
        var row = _rows.FirstOrDefault(candidate => candidate.Macro == macro);
        if (row is not null)
        {
            MacroList.SelectedItem = row;
        }
    }

    private HotkeyMacro CreateMacro()
    {
        var used = _rows.Select(row => row.Macro.Gesture).ToHashSet();

        for (var digit = 1; digit <= 9; digit++)
        {
            var candidate = new HotkeyGesture(
                HotkeyModifiers.Control | HotkeyModifiers.Alt,
                (uint)(0x30 + digit));

            if (!used.Contains(candidate))
            {
                return new HotkeyMacro
                {
                    Gesture = candidate,
                    Action = MacroAction.ActivateProfile,
                    Profile = AudioProfile.Movie,
                };
            }
        }

        return new HotkeyMacro
        {
            Gesture = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x4D),
            Action = MacroAction.ActivateProfile,
            Profile = AudioProfile.Movie,
        };
    }

    private static string Describe(HotkeyModifiers modifiers, uint virtualKey)
    {
        if (virtualKey == 0)
        {
            return string.Empty;
        }

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

        parts.Add(HotkeyGesture.KeyName(virtualKey));
        return string.Join('+', parts);
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
