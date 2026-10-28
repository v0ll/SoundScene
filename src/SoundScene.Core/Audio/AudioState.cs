using System.Collections.Immutable;

namespace SoundScene.Core.Audio;

/// <summary>
/// A snapshot of the Dolby audio state. Immutable so it can cross threads and be
/// stored in scenes without defensive copying.
/// </summary>
public sealed record AudioState
{
    public required AudioProfile Profile { get; init; }

    public int SubProfile { get; init; }

    public bool DolbyEnabled { get; init; }

    /// <summary>Dolby's "intelligent EQ" preset index.</summary>
    public int Ieq { get; init; }

    /// <summary>
    /// The 20 graphic-EQ bands, when they are meaningful for the active profile.
    /// </summary>
    /// <remarks>
    /// EQ levels are per profile. This is only populated when
    /// <see cref="Profile"/> is <see cref="AudioProfile.Personalize"/>; use
    /// <c>IAudioBackend.GetGeqAsync</c> to read them from any profile.
    /// </remarks>
    public ImmutableArray<int> GeqLevels { get; init; } = ImmutableArray<int>.Empty;

    /// <summary>Reported by the driver; 192 on this machine.</summary>
    public int GeqMaximumRange { get; init; }

    public static AudioState Unknown { get; } = new() { Profile = AudioProfile.Movie };
}
