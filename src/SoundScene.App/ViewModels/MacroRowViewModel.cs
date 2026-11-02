using SoundScene.App.Mvvm;
using SoundScene.Core.Audio;
using SoundScene.Core.Hotkeys;

namespace SoundScene.App.ViewModels;

/// <summary>One row in the hotkey list, kept in sync with the macro it shows.</summary>
public sealed class MacroRowViewModel : ObservableObject
{
    private HotkeyMacro _macro;

    public MacroRowViewModel(HotkeyMacro macro)
    {
        ArgumentNullException.ThrowIfNull(macro);
        _macro = macro;
    }

    public HotkeyMacro Macro
    {
        get => _macro;
        private set
        {
            if (SetProperty(ref _macro, value))
            {
                OnPropertyChanged(nameof(GestureLabel));
                OnPropertyChanged(nameof(ActionLabel));
            }
        }
    }

    public string GestureLabel => _macro.Enabled
        ? _macro.Gesture.DisplayName
        : _macro.Gesture.DisplayName + " (off)";

    public string ActionLabel => _macro.Action == MacroAction.ToggleProfiles
        ? $"Toggle {_macro.ProfileA.DisplayName()} \u2194 {_macro.ProfileB.DisplayName()}"
        : $"Switch to {_macro.Profile.DisplayName()}";

    public void Update(HotkeyMacro macro) => Macro = macro;
}
