namespace SoundScene.Core.Audio;

/// <summary>
/// One selectable main profile, with the index this particular DAX build uses.
/// </summary>
/// <remarks>
/// The index is the only thing that is stable across builds; the name is
/// whatever the driver reports. <see cref="KnownProfile"/> is set when the name
/// matches one of the profiles SoundScene knows about, so persistence and
/// hotkeys can keep using the enum.
/// </remarks>
public sealed record AudioProfileDescriptor(int Index, string Name, AudioProfile? KnownProfile)
{
    public string DisplayName =>
        !string.IsNullOrWhiteSpace(Name)
            ? Name
            : KnownProfile?.DisplayName() ?? $"Profile {Index + 1}";

    public string Glyph => KnownProfile?.Glyph() ?? "\uE767";
}

/// <summary>
/// What this DAX installation actually exposes.
/// </summary>
/// <remarks>
/// Dolby does not document the band count, the range, or the profile order, and
/// they are not guaranteed to be the same on every laptop. Everything the UI
/// builds is derived from this record instead of from the G14's constants.
/// </remarks>
public sealed record AudioCapabilities
{
    public int BandCount { get; init; } = GraphicEqualizer.BandCount;

    public int GeqMaximumRange { get; init; } = GraphicEqualizer.DefaultRange;

    /// <summary>Band centres read from the driver's tuning XML, when found.</summary>
    public IReadOnlyList<double>? BandFrequencies { get; init; }

    /// <summary>Where <see cref="BandFrequencies"/> came from, for diagnostics.</summary>
    public string? BandFrequencySource { get; init; }

    public IReadOnlyList<AudioProfileDescriptor> Profiles { get; init; } =
        AudioProfileCatalog.DefaultProfiles;

    public static AudioCapabilities Unknown { get; } = new();

    public AudioProfileDescriptor? ProfileAt(int index) =>
        Profiles.FirstOrDefault(profile => profile.Index == index);

    /// <summary>The real driver index for a known profile, if this build has it.</summary>
    public int? IndexOf(AudioProfile profile) =>
        Profiles.FirstOrDefault(candidate => candidate.KnownProfile == profile)?.Index;

    /// <summary>Index of Personalize, falling back to Dolby's historical value.</summary>
    public int PersonalizeIndex => IndexOf(AudioProfile.Personalize) ?? (int)AudioProfile.Personalize;
}

/// <summary>Maps driver-reported names onto the profiles SoundScene knows.</summary>
public static class AudioProfileCatalog
{
    public static IReadOnlyList<AudioProfileDescriptor> DefaultProfiles { get; } =
        Enum.GetValues<AudioProfile>()
            .Select(profile => new AudioProfileDescriptor((int)profile, profile.DisplayName(), profile))
            .ToArray();

    /// <summary>
    /// Builds a catalog from the driver's device-mode names.
    /// </summary>
    /// <remarks>
    /// Unknown names are kept, because showing the driver's own label is better
    /// than hiding a profile. If the driver returns nothing (some builds answer
    /// zero modes to unpackaged callers), the default six are used.
    /// </remarks>
    public static IReadOnlyList<AudioProfileDescriptor> FromDriverNames(IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        if (names.Count == 0 || names.All(string.IsNullOrWhiteSpace))
        {
            return DefaultProfiles;
        }

        var descriptors = new List<AudioProfileDescriptor>(names.Count);
        for (var index = 0; index < names.Count; index++)
        {
            var name = names[index]?.Trim() ?? string.Empty;
            descriptors.Add(new AudioProfileDescriptor(index, name, Match(name)));
        }

        return descriptors;
    }

    private static AudioProfile? Match(string name)
    {
        var normalized = Normalize(name);
        if (normalized.Length == 0)
        {
            return null;
        }

        foreach (var profile in Enum.GetValues<AudioProfile>())
        {
            if (Normalize(profile.DisplayName()) == normalized)
            {
                return profile;
            }
        }

        // Some builds call Dynamic "Balanced"; keep the hotkey meaning intact.
        return normalized switch
        {
            "balanced" => AudioProfile.Dynamic,
            _ => null,
        };
    }

    private static string Normalize(string value)
    {
        Span<char> buffer = stackalloc char[value.Length];
        var length = 0;

        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c))
            {
                buffer[length++] = char.ToLowerInvariant(c);
            }
        }

        return new string(buffer[..length]);
    }
}
