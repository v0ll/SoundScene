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

    /// <summary>A flat response, used for reset and as a safe default.</summary>
    public static int[] Flat() => new int[BandCount];
}
