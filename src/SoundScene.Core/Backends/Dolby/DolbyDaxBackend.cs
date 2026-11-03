using System.Collections.Immutable;
using System.Globalization;
using SoundScene.Core.Audio;

namespace SoundScene.Core.Backends.Dolby;

/// <summary>
/// Drives Dolby Access through the native bridge.
/// </summary>
/// <remarks>
/// All native calls are funnelled through a semaphore because the underlying
/// DAX session is a process-wide singleton and is not thread-safe.
/// </remarks>
public sealed class DolbyDaxBackend : IAudioBackend
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DaxBridge? _bridge;
    private AudioCapabilities _capabilities = AudioCapabilities.Unknown;
    private bool _disposed;

    public string Name => "Dolby DAX";

    public bool IsAvailable { get; private set; }

    public async Task<AudioCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureOpen();
            return _capabilities;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<AudioState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureOpen();
            return _bridge!.GetState(_capabilities);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task SetProfileAsync(AudioProfile profile, CancellationToken cancellationToken = default) =>
        SetProfileIndexAsync(_capabilities.IndexOf(profile) ?? (int)profile, cancellationToken);

    public async Task SetProfileIndexAsync(int index, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureOpen();
            _bridge!.SetProfile(index);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Reads the GEQ bands by temporarily activating Personalize.
    /// </summary>
    /// <remarks>
    /// Dolby keeps EQ levels per profile and only exposes them on the active
    /// one, so this necessarily flips profiles. It is only called when the user
    /// opens the EQ editor or saves a scene - never on a polling path.
    /// </remarks>
    public Task<IReadOnlyList<int>> GetGeqAsync(CancellationToken cancellationToken = default) =>
        OnPersonalizeAsync(bridge => (IReadOnlyList<int>)bridge.GetGeq(), cancellationToken);

    /// <summary>
    /// Writes the GEQ bands, switching to Personalize first when needed.
    /// </summary>
    /// <remarks>
    /// Dolby only accepts EQ writes while the Personalize profile is active;
    /// any other profile returns DISP_E_ARRAYISLOCKED. The previous profile is
    /// restored afterwards, which is safe because the levels are stored on the
    /// Personalize profile rather than on the active one.
    /// </remarks>
    public Task SetGeqAsync(IReadOnlyList<int> levels, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(levels);

        return OnPersonalizeAsync(
            bridge =>
            {
                bridge.SetGeq(levels);
                return true;
            },
            cancellationToken);
    }

    /// <summary>
    /// Sets the volume leveler. It is a sub-profile setting like the EQ, so the
    /// Personalize dance applies.
    /// </summary>
    public Task SetVolumeLevelerAsync(bool enabled, CancellationToken cancellationToken = default) =>
        OnPersonalizeAsync(
            bridge =>
            {
                bridge.SetVolumeLeveler(enabled);
                return true;
            },
            cancellationToken);

    /// <summary>Sets the surround virtualizer, with the same Personalize dance.</summary>
    public Task SetSurroundVirtualizerAsync(bool enabled, CancellationToken cancellationToken = default) =>
        OnPersonalizeAsync(
            bridge =>
            {
                bridge.SetSurroundVirtualizer(enabled);
                return true;
            },
            cancellationToken);

    /// <summary>
    /// Runs <paramref name="work"/> with Personalize active and restores the
    /// profile afterwards. The gate is held for the whole switch.
    /// </summary>
    private async Task<T> OnPersonalizeAsync<T>(Func<DaxBridge, T> work, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureOpen();

            var previous = _bridge!.GetProfile();
            var personalize = _capabilities.PersonalizeIndex;
            var switched = previous != personalize;

            try
            {
                if (switched)
                {
                    _bridge.SetProfile(personalize);
                }

                return work(_bridge);
            }
            finally
            {
                if (switched)
                {
                    _bridge.SetProfile(previous);
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Raw diagnostic dump, used by the diagnostics window.</summary>
    public async Task<string> GetDiagnosticsAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureOpen();

            var capabilities = string.Join(
                Environment.NewLine,
                $"band count     : {_capabilities.BandCount}",
                $"geq range      : {_capabilities.GeqMaximumRange}",
                $"personalize    : index {_capabilities.PersonalizeIndex}",
                "profiles       :",
                string.Join(
                    Environment.NewLine,
                    _capabilities.Profiles.Select(profile =>
                        $"  [{profile.Index}] {profile.DisplayName}" +
                        (profile.KnownProfile is null ? " (unmapped)" : string.Empty))),
                $"band frequencies: {(_capabilities.BandFrequencies is null ? "<unknown>" : string.Join(", ", _capabilities.BandFrequencies.Select(value => value.ToString("0", CultureInfo.InvariantCulture))))}",
                $"tuning source  : {_capabilities.BandFrequencySource ?? "<not found>"}");

            return string.Join(
                Environment.NewLine,
                "Packages:",
                DaxPackages.Describe(),
                string.Empty,
                "Capabilities:",
                capabilities,
                string.Empty,
                "Session:",
                _bridge!.DumpState());
        }
        finally
        {
            _gate.Release();
        }
    }

    private void EnsureOpen()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_bridge is not null)
        {
            return;
        }

        var cache = DaxPackageCache.Ensure(out var error);
        if (!string.IsNullOrEmpty(error))
        {
            throw new AudioBackendException(error);
        }

        var bridge = new DaxBridge();
        try
        {
            bridge.Open(cache);
            _capabilities = ReadCapabilities(bridge);
        }
        catch
        {
            bridge.Dispose();
            throw;
        }

        _bridge = bridge;
        IsAvailable = true;
    }

    /// <summary>
    /// Probes the session for its real width and profile list. A failed probe
    /// is not fatal: the defaults keep the UI usable and diagnostics show why.
    /// </summary>
    private static AudioCapabilities ReadCapabilities(DaxBridge bridge)
    {
        try
        {
            var (bandCount, maxRange, names) = bridge.GetCapabilities();
            var tuning = DaxTuning.TryRead(bandCount);

            return new AudioCapabilities
            {
                BandCount = bandCount,
                GeqMaximumRange = maxRange,
                Profiles = AudioProfileCatalog.FromDriverNames(names),
                BandFrequencies = tuning?.Frequencies,
                BandFrequencySource = tuning?.Source,
            };
        }
        catch (AudioBackendException)
        {
            return AudioCapabilities.Unknown;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _bridge?.Dispose();
            _bridge = null;
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }
}
