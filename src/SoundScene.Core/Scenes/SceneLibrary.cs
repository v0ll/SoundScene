using System.Collections.Immutable;
using SoundScene.Core.Audio;

namespace SoundScene.Core.Scenes;

/// <summary>
/// Capture, apply, and persist scenes.
/// </summary>
/// <remarks>
/// Applying writes the EQ before the profile. <c>SetGeqAsync</c> internally
/// switches to Personalize and restores whatever was active, so the final
/// profile write is what the user actually ends up hearing.
/// </remarks>
public sealed class SceneLibrary
{
    private readonly IAudioBackend _backend;
    private readonly SceneStore _store;
    private readonly List<Scene> _scenes = [];

    public SceneLibrary(IAudioBackend backend, SceneStore? store = null)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _store = store ?? new SceneStore();
    }

    public IReadOnlyList<Scene> Scenes => _scenes;

    public void Load()
    {
        _scenes.Clear();
        _scenes.AddRange(_store.Load());
    }

    /// <summary>Snapshots the current state under a unique name.</summary>
    public async Task<Scene> CaptureAsync(string name, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var state = await _backend.GetStateAsync(cancellationToken).ConfigureAwait(false);

        var levels = state.Profile == AudioProfile.Personalize
            ? (await _backend.GetGeqAsync(cancellationToken).ConfigureAwait(false)).ToImmutableArray()
            : ImmutableArray<int>.Empty;

        var scene = new Scene
        {
            Name = MakeUniqueName(name.Trim()),
            Profile = state.Profile,
            GeqLevels = levels,
            Ieq = state.Ieq,
        };

        _scenes.Add(scene);
        _store.Save(_scenes);
        return scene;
    }

    public bool Remove(string name)
    {
        var removed = _scenes.RemoveAll(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        if (removed == 0)
        {
            return false;
        }

        _store.Save(_scenes);
        return true;
    }

    public async Task ApplyAsync(Scene scene, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scene);

        if (scene.HasCustomEq)
        {
            await _backend.SetGeqAsync(scene.GeqLevels, cancellationToken).ConfigureAwait(false);
        }

        await _backend.SetProfileAsync(scene.Profile, cancellationToken).ConfigureAwait(false);
    }

    private string MakeUniqueName(string name)
    {
        if (!_scenes.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            return name;
        }

        for (var suffix = 2; suffix < 1000; suffix++)
        {
            var candidate = $"{name} ({suffix})";
            if (!_scenes.Any(s => string.Equals(s.Name, candidate, StringComparison.OrdinalIgnoreCase)))
            {
                return candidate;
            }
        }

        return $"{name} ({Guid.NewGuid():N})";
    }
}
