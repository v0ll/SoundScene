namespace SoundScene.Core.Audio;

/// <summary>
/// Main Dolby Access profiles, in the order the DAX API indexes them.
/// </summary>
/// <remarks>
/// Verified on Dolby Access 3.27.12250.0 / DAX 3.30705.750.0:
/// index 0 is Movie and index 4 is Personalize. The values in between are
/// inferred from the Dolby Access UI ordering and the names in Runtime.xml.
/// </remarks>
public enum AudioProfile
{
    Movie = 0,
    Music = 1,
    Game = 2,
    Voice = 3,
    Personalize = 4,
    Dynamic = 5,
}

public static class AudioProfileExtensions
{
    public static string DisplayName(this AudioProfile profile) => profile switch
    {
        AudioProfile.Movie => "Movie",
        AudioProfile.Music => "Music",
        AudioProfile.Game => "Game",
        AudioProfile.Voice => "Voice",
        AudioProfile.Personalize => "Personalize",
        AudioProfile.Dynamic => "Dynamic",
        _ => profile.ToString(),
    };

    public static string Glyph(this AudioProfile profile) => profile switch
    {
        AudioProfile.Movie => "\uE714",
        AudioProfile.Music => "\uE8D6",
        AudioProfile.Game => "\uE7FC",
        AudioProfile.Voice => "\uE720",
        AudioProfile.Personalize => "\uE9E9",
        AudioProfile.Dynamic => "\uE9D9",
        _ => "\uE767",
    };
}
