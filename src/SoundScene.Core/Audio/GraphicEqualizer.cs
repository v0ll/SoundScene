using System.Globalization;

namespace SoundScene.Core.Audio;

/// <summary>
/// Layout of the 20-band graphic equalizer Dolby exposes through DAX.
/// </summary>
/// <remarks>
/// The DAX API only gives you 20 unlabelled values, so the band centers come
/// from the driver's own tuning XML:
/// <c>&lt;band_20_freq fs_48000="47,141,234,...,19688"/&gt;</c> in the Realtek
/// DAX3 extension package. They are FFT-bin centers rather than round numbers,
/// which is why 1031 Hz shows up instead of 1000 Hz.
/// </remarks>
public static class GraphicEqualizer
{
    /// <summary>Band count. Dolby does not expose this, but every DAX build uses 20.</summary>
    public const int BandCount = 20;

    /// <summary>Default per-band range in 1/16 dB, matching <c>GetGEQMaximumRange</c>.</summary>
    public const int DefaultRange = 192;

    /// <summary>Center frequency of each band in Hz, low to high, at 48 kHz.</summary>
    public static IReadOnlyList<double> Frequencies { get; } =
    [
        47, 141, 234, 328, 469, 656, 844, 1031, 1313, 1688,
        2250, 3000, 3750, 4688, 5813, 7125, 9000, 11250, 13875, 19688,
    ];

    /// <summary>Short axis label, e.g. <c>47</c>, <c>1.3k</c>, <c>20k</c>.</summary>
    public static string FormatFrequency(double hertz)
    {
        if (hertz < 1000)
        {
            return hertz.ToString("0", CultureInfo.InvariantCulture);
        }

        var kilohertz = hertz / 1000;
        var rounded = kilohertz < 10
            ? Math.Round(kilohertz, 1, MidpointRounding.AwayFromZero)
            : Math.Round(kilohertz, 0, MidpointRounding.AwayFromZero);

        return rounded.ToString("0.#", CultureInfo.InvariantCulture) + "k";
    }

    /// <summary>Converts a band value in 1/16 dB to decibels.</summary>
    public static double ToDecibels(int value) => value / 16.0;

    /// <summary>Clamps a raw band value to the range the driver reports.</summary>
    public static int Clamp(double value, int range) =>
        (int)Math.Round(Math.Clamp(value, -range, range), MidpointRounding.AwayFromZero);

    /// <summary>
    /// How close a value has to get to 0 before it snaps there: 8 units, i.e.
    /// half a decibel. A 1/16 dB step is far finer than a hand can hit, so
    /// without this nobody can land on a flat band.
    /// </summary>
    public const int FlatSnap = 8;

    /// <summary>Snaps near-zero values to exactly flat.</summary>
    public static int Snap(int value) => Math.Abs(value) <= FlatSnap ? 0 : value;

    /// <summary>Signed one-decimal dB readout, e.g. <c>+3.0</c>, <c>0.0</c>.</summary>
    public static string FormatDecibels(double decibels) =>
        decibels.ToString("+0.0;-0.0;0.0", CultureInfo.InvariantCulture);

    /// <summary>
    /// The simple view's ten sliders. Each one moves a pair of adjacent driver
    /// bands; the label is the pair's geometric centre, so it is honest about
    /// what the slider covers instead of pretending to be a 1-octave EQ.
    /// </summary>
    public static IReadOnlyList<EqualizerGroup> SimpleGroups { get; } =
        Enumerable.Range(0, BandCount / 2)
            .Select(pair => new EqualizerGroup(
                pair * 2,
                (pair * 2) + 1,
                Math.Sqrt(Frequencies[pair * 2] * Frequencies[(pair * 2) + 1])))
            .ToArray();

    /// <summary>A flat response, used for reset and as a safe default.</summary>
    public static int[] Flat() => new int[BandCount];
}

/// <summary>Two adjacent driver bands driven by one slider in the simple view.</summary>
public sealed record EqualizerGroup(int LowIndex, int HighIndex, double CenterFrequency)
{
    public string Label => GraphicEqualizer.FormatFrequency(CenterFrequency);

    /// <summary>The exact driver bands this slider writes, e.g. <c>47 Hz + 141 Hz</c>.</summary>
    public string Members =>
        $"{GraphicEqualizer.Frequencies[LowIndex].ToString("0", CultureInfo.InvariantCulture)} Hz + " +
        $"{GraphicEqualizer.Frequencies[HighIndex].ToString("0", CultureInfo.InvariantCulture)} Hz";
}
