using System.Collections.Immutable;
using SoundScene.Core.Audio;

namespace SoundScene.Core.Scenes;

/// <summary>
/// A named snapshot of the audio state.
/// </summary>
/// <remarks>
/// A scene always stores the active profile. It only stores EQ bands when the
/// snapshot was taken on Personalize, because Dolby keeps EQ per profile and
/// only applies it there - capturing zeros from Movie would be a lie.
/// </remarks>
public sealed record Scene
{
    public required string Name { get; init; }

    public required AudioProfile Profile { get; init; }

    /// <summary>
    /// The driver index that was active when the scene was captured.
    /// </summary>
    /// <remarks>
    /// Obsolete files only have <see cref="Profile"/>, so this is nullable. New
    /// captures use the index because a future Dolby build could reorder modes.
    /// </remarks>
    public int? ProfileIndex { get; init; }

    /// <summary>The driver's own label at capture time, if it reported one.</summary>
    public string? ProfileName { get; init; }

    public ImmutableArray<int> GeqLevels { get; init; } = ImmutableArray<int>.Empty;

    public int Ieq { get; init; }

    public bool HasCustomEq => !GeqLevels.IsDefaultOrEmpty;

    public string DisplayName =>
        string.IsNullOrWhiteSpace(ProfileName) ? Profile.DisplayName() : ProfileName;

    public string Summary => HasCustomEq
        ? $"{DisplayName} \u00B7 custom EQ"
        : DisplayName;
}
