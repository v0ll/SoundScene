using SoundScene.Core.Audio;
using SoundScene.Core.Hotkeys;

namespace SoundScene.App.ViewModels;

/// <summary>Choice shown in the macro editor's action combo.</summary>
public sealed record ActionOption(MacroAction Action, string Name)
{
    // The combo shows Name; without this, UIA announces the record's ToString.
    public override string ToString() => Name;
}

/// <summary>Choice shown in the macro editor's profile combos.</summary>
public sealed record ProfileOption(AudioProfile Profile, string Name)
{
    public override string ToString() => Name;
}
