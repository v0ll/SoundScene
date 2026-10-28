using System.Collections.Immutable;
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
    private bool _disposed;

    public string Name => "Dolby DAX";

    public bool IsAvailable { get; private set; }

    public async Task<AudioState> GetStateAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureOpen();
            return _bridge!.GetState();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetProfileAsync(AudioProfile profile, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureOpen();
            _bridge!.SetProfile((int)profile);
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
    public async Task<IReadOnlyList<int>> GetGeqAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureOpen();

            var previous = (AudioProfile)_bridge!.GetProfile();
            var switched = previous != AudioProfile.Personalize;

            try
            {
                if (switched)
                {
                    _bridge.SetProfile((int)AudioProfile.Personalize);
                }

                return _bridge.GetGeq();
            }
            finally
            {
                if (switched)
                {
                    _bridge.SetProfile((int)previous);
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Writes the GEQ bands, switching to Personalize first when needed.
    /// </summary>
    /// <remarks>
    /// Dolby only accepts EQ writes while the Personalize profile is active;
    /// any other profile returns DISP_E_ARRAYISLOCKED. The previous profile is
    /// restored afterwards, which is safe because the levels are stored on the
    /// Personalize profile rather than on the active one.
    /// </remarks>
    public async Task SetGeqAsync(IReadOnlyList<int> levels, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(levels);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureOpen();

            var previous = (AudioProfile)_bridge!.GetProfile();
            var switched = previous != AudioProfile.Personalize;

            try
            {
                if (switched)
                {
                    _bridge.SetProfile((int)AudioProfile.Personalize);
                }

                _bridge.SetGeq(levels);
            }
            finally
            {
                if (switched)
                {
                    _bridge.SetProfile((int)previous);
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
            return _bridge!.DumpState();
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
        }
        catch
        {
            bridge.Dispose();
            throw;
        }

        _bridge = bridge;
        IsAvailable = true;
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
