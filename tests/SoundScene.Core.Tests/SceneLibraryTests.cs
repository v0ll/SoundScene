using SoundScene.Core.Audio;
using SoundScene.Core.Backends;
using SoundScene.Core.Scenes;

namespace SoundScene.Core.Tests;

public class SceneLibraryTests
{
    private static (SceneLibrary Library, MockBackend Backend, string Path) Create()
    {
        var path = Path.Combine(Path.GetTempPath(), $"soundscene-scenes-{Guid.NewGuid():N}.json");
        var backend = new MockBackend();
        var library = new SceneLibrary(backend, new SceneStore(path));
        return (library, backend, path);
    }

    [Fact]
    public async Task Capture_OnMovie_StoresNoEq()
    {
        var (library, backend, path) = Create();

        try
        {
            await backend.SetProfileAsync(AudioProfile.Movie);
            var scene = await library.CaptureAsync("Movie");

            Assert.Equal(AudioProfile.Movie, scene.Profile);
            Assert.False(scene.HasCustomEq);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Capture_OnPersonalize_StoresEq()
    {
        var (library, backend, path) = Create();

        try
        {
            await backend.SetProfileAsync(AudioProfile.Personalize);
            await backend.SetGeqAsync(Enumerable.Repeat(30, 20).ToArray());

            var scene = await library.CaptureAsync("Bassy");

            Assert.True(scene.HasCustomEq);
            Assert.All(scene.GeqLevels, value => Assert.Equal(30, value));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Capture_DuplicateName_GetsSuffix()
    {
        var (library, backend, path) = Create();

        try
        {
            await backend.SetProfileAsync(AudioProfile.Movie);

            var first = await library.CaptureAsync("Night");
            var second = await library.CaptureAsync("Night");

            Assert.Equal("Night", first.Name);
            Assert.Equal("Night (2)", second.Name);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Apply_SetsProfileAndEq()
    {
        var (library, backend, path) = Create();

        try
        {
            await backend.SetProfileAsync(AudioProfile.Personalize);
            await backend.SetGeqAsync(Enumerable.Repeat(12, 20).ToArray());
            var scene = await library.CaptureAsync("Warm");

            await backend.SetProfileAsync(AudioProfile.Movie);
            await backend.SetGeqAsync(new int[20]);

            await library.ApplyAsync(scene);

            var state = await backend.GetStateAsync();
            Assert.Equal(AudioProfile.Personalize, state.Profile);

            var levels = await backend.GetGeqAsync();
            Assert.All(levels, value => Assert.Equal(12, value));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Remove_PersistsAcrossReload()
    {
        var (library, backend, path) = Create();

        try
        {
            await backend.SetProfileAsync(AudioProfile.Movie);
            await library.CaptureAsync("One");
            await library.CaptureAsync("Two");

            Assert.True(library.Remove("One"));

            var reloaded = new SceneLibrary(backend, new SceneStore(path));
            reloaded.Load();

            var scene = Assert.Single(reloaded.Scenes);
            Assert.Equal("Two", scene.Name);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Scenes_SurviveReload()
    {
        var (library, backend, path) = Create();

        try
        {
            await backend.SetProfileAsync(AudioProfile.Game);
            await library.CaptureAsync("Shooter");

            var reloaded = new SceneLibrary(backend, new SceneStore(path));
            reloaded.Load();

            var scene = Assert.Single(reloaded.Scenes);
            Assert.Equal("Shooter", scene.Name);
            Assert.Equal(AudioProfile.Game, scene.Profile);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
