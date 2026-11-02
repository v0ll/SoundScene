using SoundScene.Core.Audio;
using SoundScene.Core.Backends;

namespace SoundScene.Core.Tests;

public class MockBackendTests
{
    [Fact]
    public async Task SetProfileAsync_UpdatesState()
    {
        var backend = new MockBackend();

        await backend.SetProfileAsync(AudioProfile.Game);
        var state = await backend.GetStateAsync();

        Assert.Equal(AudioProfile.Game, state.Profile);
    }

    [Fact]
    public async Task SetProfileAsync_RecordsHistory()
    {
        var backend = new MockBackend();

        await backend.SetProfileAsync(AudioProfile.Music);
        await backend.SetProfileAsync(AudioProfile.Movie);

        Assert.Equal([AudioProfile.Music, AudioProfile.Movie], backend.ProfileHistory);
    }

    [Fact]
    public async Task SetGeqAsync_WritesLevels()
    {
        var backend = new MockBackend();
        var levels = Enumerable.Range(0, 20).Select(i => i * 4).ToArray();

        await backend.SetGeqAsync(levels);
        var readBack = await backend.GetGeqAsync();

        Assert.Equal(levels, readBack);
    }

    [Fact]
    public async Task GetStateAsync_OnlyExposesEqUnderPersonalize()
    {
        var backend = new MockBackend();
        await backend.SetProfileAsync(AudioProfile.Movie);

        var state = await backend.GetStateAsync();

        Assert.Empty(state.GeqLevels);
    }

    [Fact]
    public async Task SetGeqAsync_SwitchesToPersonalizeAndBack()
    {
        var backend = new MockBackend();
        await backend.SetProfileAsync(AudioProfile.Movie);
        backend.ProfileHistory.Clear();

        await backend.SetGeqAsync(new int[20]);

        Assert.Equal([AudioProfile.Personalize, AudioProfile.Movie], backend.ProfileHistory);
    }

    [Fact]
    public async Task SetGeqAsync_RejectsWrongBandCount()
    {
        var backend = new MockBackend();

        await Assert.ThrowsAsync<AudioBackendException>(() => backend.SetGeqAsync(new int[10]));
    }

    [Fact]
    public async Task SetVolumeLevelerAsync_RoundTrips()
    {
        var backend = new MockBackend();

        await backend.SetVolumeLevelerAsync(true);
        var state = await backend.GetStateAsync();

        Assert.True(state.VolumeLevelerEnabled);
    }

    [Fact]
    public async Task SetSurroundVirtualizerAsync_RoundTrips()
    {
        var backend = new MockBackend();

        await backend.SetSurroundVirtualizerAsync(true);
        var state = await backend.GetStateAsync();

        Assert.True(state.SurroundVirtualizerEnabled);
    }
}
