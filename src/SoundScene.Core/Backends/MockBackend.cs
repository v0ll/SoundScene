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
    private readonly AudioCapabilities _capabilities;
    private AudioState _state = new()
    {
        Profile = AudioProfile.Movie,
        ProfileIndex = (int)AudioProfile.Movie,
        ProfileName = AudioProfile.Movie.DisplayName(),
        DolbyEnabled = true,
        Ieq = 0,
        GeqLevels = ImmutableArray.Create(new int[GraphicEqualizer.BandCount]),
        GeqMaximumRange = GraphicEqualizer.DefaultRange,
    };

    /// <summary>Creates a 20-band mock, or one shaped like another DAX build.</summary>
    public MockBackend(
        int bandCount = GraphicEqualizer.BandCount,
        int geqMaximumRange = GraphicEqualizer.DefaultRange)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bandCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(geqMaximumRange);

        _capabilities = new AudioCapabilities
        {
            BandCount = bandCount,
            GeqMaximumRange = geqMaximumRange,
            BandFrequencies = bandCount == GraphicEqualizer.BandCount
                ? GraphicEqualizer.Frequencies
                : null,
        };

        _state = _state with
        {
            GeqLevels = ImmutableArray.Create(new int[bandCount]),
            GeqMaximumRange = geqMaximumRange,
        };
    }

    public string Name => "Mock";

    public bool IsAvailable => true;

    /// <summary>Records every profile that was ever written, for assertions.</summary>
    public List<AudioProfile> ProfileHistory { get; } = [];

    public Task<AudioCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_capabilities);

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

    public Task SetProfileAsync(AudioProfile profile, CancellationToken cancellationToken = default) =>
        SetProfileIndexAsync((int)profile, cancellationToken);

    public Task SetProfileIndexAsync(int index, CancellationToken cancellationToken = default)
    {
        var profile = (AudioProfile)index;

        lock (_sync)
        {
            _state = _state with
            {
                Profile = profile,
                ProfileIndex = index,
                ProfileName = profile.DisplayName(),
            };
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
            _state = _state with
            {
                Profile = AudioProfile.Personalize,
                ProfileIndex = (int)AudioProfile.Personalize,
                ProfileName = AudioProfile.Personalize.DisplayName(),
            };
            ProfileHistory.Add(AudioProfile.Personalize);

            _state = _state with { GeqLevels = levels.ToImmutableArray() };

            _state = _state with
            {
                Profile = previous,
                ProfileIndex = (int)previous,
                ProfileName = previous.DisplayName(),
            };
            ProfileHistory.Add(previous);
        }

        return Task.CompletedTask;
    }

    public Task SetVolumeLevelerAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            _state = _state with { VolumeLevelerEnabled = enabled };
        }

        return Task.CompletedTask;
    }

    public Task SetSurroundVirtualizerAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            _state = _state with { SurroundVirtualizerEnabled = enabled };
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public Task<string> GetDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var text = $"""
                backend        : Mock (in-memory, no audio hardware)
                profile        : {_state.ProfileName ?? _state.Profile.DisplayName()} (index {_state.ProfileIndex})
                sub-profile    : {_state.SubProfile}
                dolby enabled  : {_state.DolbyEnabled}
                ieq            : {_state.Ieq}
                volume leveler : {_state.VolumeLevelerEnabled}
                surround       : {_state.SurroundVirtualizerEnabled}
                band count     : {_state.GeqLevels.Length}
                geq range      : {_state.GeqMaximumRange}
                geq bands      : {string.Join(",", _state.GeqLevels)}
                """;

            return Task.FromResult(text);
        }
    }
}
