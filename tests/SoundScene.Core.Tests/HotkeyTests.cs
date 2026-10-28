using SoundScene.Core.Audio;
using SoundScene.Core.Hotkeys;
using SoundScene.Core.Settings;

namespace SoundScene.Core.Tests;

public class HotkeyGestureTests
{
    [Fact]
    public void DisplayName_OrdersModifiersDeterministically()
    {
        var gesture = new HotkeyGesture(
            HotkeyModifiers.Win | HotkeyModifiers.Shift | HotkeyModifiers.Alt | HotkeyModifiers.Control,
            0x4D);

        Assert.Equal("Ctrl+Alt+Shift+Win+M", gesture.DisplayName);
    }

    [Fact]
    public void DisplayName_FormatsFunctionKeys()
    {
        var gesture = new HotkeyGesture(HotkeyModifiers.Control, 0x70);

        Assert.Equal("Ctrl+F1", gesture.DisplayName);
    }

    [Fact]
    public void DisplayName_FormatsDigits()
    {
        var gesture = new HotkeyGesture(HotkeyModifiers.Alt, 0x31);

        Assert.Equal("Alt+1", gesture.DisplayName);
    }

    [Fact]
    public void Constructor_RejectsZeroVirtualKey()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HotkeyGesture(HotkeyModifiers.Control, 0));
    }
}

public class ToggleMacroTests
{
    private static ToggleMacro CreateMacro() => new()
    {
        Gesture = new HotkeyGesture(HotkeyModifiers.Control | HotkeyModifiers.Alt, 0x4D),
        ProfileA = AudioProfile.Movie,
        ProfileB = AudioProfile.Game,
    };

    [Fact]
    public void Next_FromA_ReturnsB()
    {
        var macro = CreateMacro();

        Assert.Equal(AudioProfile.Game, macro.Next(AudioProfile.Movie));
    }

    [Fact]
    public void Next_FromB_ReturnsA()
    {
        var macro = CreateMacro();

        Assert.Equal(AudioProfile.Movie, macro.Next(AudioProfile.Game));
    }

    [Fact]
    public void Next_FromAnyOtherProfile_ReturnsA()
    {
        var macro = CreateMacro();

        Assert.Equal(AudioProfile.Movie, macro.Next(AudioProfile.Music));
    }
}

public class SettingsStoreTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsMacros()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soundscene-settings-{Guid.NewGuid():N}.json");

        try
        {
            var store = new SettingsStore(path);
            var settings = AppSettings.CreateDefault();
            settings.Macros[0] = settings.Macros[0] with
            {
                ProfileA = AudioProfile.Music,
                ProfileB = AudioProfile.Personalize,
            };

            store.Save(settings);
            var loaded = new SettingsStore(path).Load();

            var macro = Assert.Single(loaded.Macros);
            Assert.Equal(AudioProfile.Music, macro.ProfileA);
            Assert.Equal(AudioProfile.Personalize, macro.ProfileB);
            Assert.Equal("Ctrl+Alt+M", macro.Gesture.DisplayName);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soundscene-missing-{Guid.NewGuid():N}.json");

        var settings = new SettingsStore(path).Load();

        Assert.Equal(AppSettings.CurrentVersion, settings.Version);
        Assert.Single(settings.Macros);
    }

    [Fact]
    public void Load_CorruptFile_ReturnsDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soundscene-corrupt-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(path, "{ this is not json");

            var settings = new SettingsStore(path).Load();

            Assert.Single(settings.Macros);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
