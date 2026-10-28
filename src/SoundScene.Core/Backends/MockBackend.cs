using System.Collections.Immutable;
using SoundScene.Core.Audio;

namespace SoundScene.Core.Backends;

/// <summary>
/// In-memory backend for tests, UI work, and machines without Dolby.
/// Behaves like the real thing, including the Personalize rule for EQ writes.
/// </summary>
public sealed class MockBackend : IAudioBackend
{
    private readonly object _sync = new();
    private AudioState _state = new()
    {
        Profile = AudioProfile.Movie,
        DolbyEnabled = true,
        Ieq = 0,
        GeqLevels = ImmutableArray.Create(new int[20]),
        GeqMaximumRange = 192,
    };

    public string Name => "Mock";

    public bool IsAvailable => true;

    /// <summary>Records every profile that was ever written, for assertions.</summary>
    public List<AudioProfile> ProfileHistory { get; } = [];

    public Task<AudioState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var state = _state.Profile == AudioProfile.Personalize
                ? _state
                : _state with { GeqLevels = ImmutableArray<int>.Empty };

            return Task.FromResult(state);
        }
    }

    public Task<IReadOnlyList<int>> GetGeqAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            IReadOnlyList<int> levels = _state.GeqLevels.ToArray();
            return Task.FromResult(levels);
        }
    }

    public Task SetProfileAsync(AudioProfile profile, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            _state = _state with { Profile = profile };
            ProfileHistory.Add(profile);
        }

        return Task.CompletedTask;
    }

    public Task SetGeqAsync(IReadOnlyList<int> levels, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(levels);

        lock (_sync)
        {
            if (levels.Count != _state.GeqLevels.Length)
            {
                throw new AudioBackendException(
                    $"Expected {_state.GeqLevels.Length} bands but got {levels.Count}.");
            }

            // Mirror the real backend: the write is attributed to Personalize.
            var previous = _state.Profile;
            _state = _state with { Profile = AudioProfile.Personalize };
            ProfileHistory.Add(AudioProfile.Personalize);

            _state = _state with { GeqLevels = levels.ToImmutableArray() };

            _state = _state with { Profile = previous };
            ProfileHistory.Add(previous);
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
