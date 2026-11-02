namespace SoundScene.Core.Audio;

/// <summary>
/// The single seam between the app and whatever is actually driving the sound
/// card. Implementations must be safe to call from any thread.
/// </summary>
public interface IAudioBackend : IAsyncDisposable
{
    /// <summary>Human-readable name, shown in diagnostics.</summary>
    string Name { get; }

    /// <summary>False when the backend cannot be used on this machine right now.</summary>
    bool IsAvailable { get; }

    Task<AudioState> GetStateAsync(CancellationToken cancellationToken = default);

    Task SetProfileAsync(AudioProfile profile, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the 20 graphic-EQ bands of the Personalize profile.
    /// </summary>
    /// <remarks>
    /// EQ levels are stored per profile, and Dolby only exposes them while
    /// Personalize is active. Implementations must therefore switch to
    /// Personalize, read, and restore the previous profile.
    /// </remarks>
    Task<IReadOnlyList<int>> GetGeqAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the 20 graphic-EQ bands. Implementations are responsible for the
    /// profile dance: Dolby only accepts EQ writes while Personalize is active.
    /// </summary>
    Task SetGeqAsync(IReadOnlyList<int> levels, CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns Dolby's volume leveler on or off.
    /// </summary>
    /// <remarks>
    /// The switch is stored on the Personalize sub-profile, like the EQ, so
    /// implementations must apply the same switch-write-restore dance.
    /// </remarks>
    Task SetVolumeLevelerAsync(bool enabled, CancellationToken cancellationToken = default);

    /// <summary>Turns Dolby's surround virtualizer on or off.</summary>
    Task SetSurroundVirtualizerAsync(bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Human-readable dump of the backend's real state, for bug reports.
    /// </summary>
    /// <remarks>
    /// Implementations should include anything that has ever explained a
    /// failure: versions, endpoints, raw API results. It is never parsed.
    /// </remarks>
    Task<string> GetDiagnosticsAsync(CancellationToken cancellationToken = default);
}
