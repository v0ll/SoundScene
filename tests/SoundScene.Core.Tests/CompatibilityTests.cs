using SoundScene.Core.Audio;
using SoundScene.Core.Backends;
using SoundScene.Core.Backends.Dolby;
using SoundScene.Core.Scenes;

namespace SoundScene.Core.Tests;

public class CompatibilityTests
{
    [Fact]
    public void CreateLayout_UsesKnownCentresForTwentyBands()
    {
        var layout = GraphicEqualizer.CreateLayout(20);

        Assert.True(layout.HasFrequencies);
        Assert.Equal(20, layout.Bands.Count);
        Assert.Equal(47, layout.Bands[0].Frequency);
        Assert.Equal(19688, layout.Bands[^1].Frequency);
        Assert.Equal("81", layout.Groups[0].Label);
    }

    [Fact]
    public void CreateLayout_UsesDriverFrequenciesWhenTheyMatch()
    {
        var frequencies = new double[] { 30, 60, 120, 240, 480, 960, 1920, 3840, 7680, 15360 };

        var layout = GraphicEqualizer.CreateLayout(10, frequencies);

        Assert.True(layout.HasFrequencies);
        Assert.Equal(10, layout.Bands.Count);
        Assert.Equal(5, layout.Groups.Count);
        Assert.Equal("30", layout.Bands[0].Label);
        Assert.Equal("42", layout.Groups[0].Label); // geometric centre of 30 and 60
        Assert.Equal("30 Hz + 60 Hz", layout.Groups[0].Members);
    }

    [Fact]
    public void CreateLayout_LeavesUnknownWidthsUnlabelled()
    {
        var layout = GraphicEqualizer.CreateLayout(7);

        Assert.False(layout.HasFrequencies);
        Assert.Equal(7, layout.Bands.Count);
        Assert.Equal("B1", layout.Bands[0].Label);
        Assert.Equal("Bands 1 + 2", layout.Groups[0].Members);

        // An odd band count leaves the last slider driving one band.
        Assert.Equal(4, layout.Groups.Count);
        Assert.Equal(6, layout.Groups[^1].LowIndex);
        Assert.Equal(6, layout.Groups[^1].HighIndex);
        Assert.Equal("Band 7", layout.Groups[^1].Members);
    }

    [Fact]
    public void CreateLayout_IgnoresMismatchedFrequencyTables()
    {
        var layout = GraphicEqualizer.CreateLayout(10, GraphicEqualizer.Frequencies);

        Assert.False(layout.HasFrequencies);
        Assert.All(layout.Bands, band => Assert.Null(band.Frequency));
    }

    [Fact]
    public void ParseBandFrequencies_ReadsThe48kAttribute()
    {
        const string xml = """
            <root>
              <band_3_freq fs_44100="43,129" fs_48000="47,141,234"/>
            </root>
            """;

        var frequencies = DaxTuning.ParseBandFrequencies(xml, 3);

        Assert.Equal(new double[] { 47, 141, 234 }, frequencies);
    }

    [Fact]
    public void ParseBandFrequencies_FallsBackTo44k()
    {
        const string xml = """<band_2_freq fs_44100="43,129"/>""";

        var frequencies = DaxTuning.ParseBandFrequencies(xml, 2);

        Assert.Equal(new double[] { 43, 129 }, frequencies);
    }

    [Fact]
    public void ParseBandFrequencies_RejectsWrongCountsAndMissingData()
    {
        Assert.Null(DaxTuning.ParseBandFrequencies("<band_2_freq fs_48000=\"1,2,3\"/>", 2));
        Assert.Null(DaxTuning.ParseBandFrequencies("<band_2_freq fs_48000=\"1\"/>", 2));
        Assert.Null(DaxTuning.ParseBandFrequencies("<other/>", 2));
        Assert.Null(DaxTuning.ParseBandFrequencies(string.Empty, 2));
    }

    [Fact]
    public void Catalog_FallsBackToTheSixKnownProfiles()
    {
        var profiles = AudioProfileCatalog.FromDriverNames([]);

        Assert.Equal(6, profiles.Count);
        Assert.Equal(
            AudioProfileCatalog.DefaultProfiles.Select(profile => profile.KnownProfile),
            profiles.Select(profile => profile.KnownProfile));
    }

    [Fact]
    public void Catalog_FallsBackWhenNamesAreBlank()
    {
        var profiles = AudioProfileCatalog.FromDriverNames(["", " "]);

        Assert.Equal(
            AudioProfileCatalog.DefaultProfiles.Select(profile => profile.KnownProfile),
            profiles.Select(profile => profile.KnownProfile));
    }

    [Fact]
    public void Catalog_KeepsDriverOrderAndIndices()
    {
        var profiles = AudioProfileCatalog.FromDriverNames(
            ["Game", "Movie", "Music", "Voice", "Personalize", "Dynamic"]);

        Assert.Equal(6, profiles.Count);
        Assert.Equal(AudioProfile.Game, profiles[0].KnownProfile);
        Assert.Equal(0, profiles[0].Index);
        Assert.Equal(AudioProfile.Movie, profiles[1].KnownProfile);
        Assert.Equal(1, profiles[1].Index);
    }

    [Fact]
    public void Catalog_MapsBalancedToDynamicAndKeepsUnknownModes()
    {
        var profiles = AudioProfileCatalog.FromDriverNames(["Movie", "Balanced", "Late Night"]);

        Assert.Equal(AudioProfile.Dynamic, profiles[1].KnownProfile);
        Assert.Null(profiles[2].KnownProfile);
        Assert.Equal("Late Night", profiles[2].DisplayName);
    }

    [Fact]
    public void Capabilities_ResolvePersonalizeFromTheRealOrder()
    {
        var capabilities = new AudioCapabilities
        {
            Profiles = AudioProfileCatalog.FromDriverNames(
                ["Movie", "Music", "Game", "Voice", "Dynamic", "Personalize"]),
        };

        Assert.Equal(5, capabilities.PersonalizeIndex);
        Assert.Equal(4, capabilities.IndexOf(AudioProfile.Dynamic));
        Assert.Equal(2, capabilities.IndexOf(AudioProfile.Game));
    }

    [Fact]
    public async Task NarrowBackend_CapturesAndAppliesItsOwnWidth()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soundscene-narrow-{Guid.NewGuid():N}.json");
        await using var backend = new MockBackend(bandCount: 10, geqMaximumRange: 96);
        var library = new SceneLibrary(backend, new SceneStore(path));

        try
        {
            var capabilities = await backend.GetCapabilitiesAsync();
            Assert.Equal(10, capabilities.BandCount);
            Assert.Equal(96, capabilities.GeqMaximumRange);

            var curve = Enumerable.Range(1, 10).ToArray();

            await backend.SetProfileAsync(AudioProfile.Personalize);
            await backend.SetGeqAsync(curve);
            var scene = await library.CaptureAsync("Narrow");

            Assert.Equal(10, scene.GeqLevels.Length);
            Assert.Equal((int)AudioProfile.Personalize, scene.ProfileIndex);

            await backend.SetGeqAsync(new int[10]);
            await library.ApplyAsync(scene);

            Assert.Equal(curve, await backend.GetGeqAsync());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
