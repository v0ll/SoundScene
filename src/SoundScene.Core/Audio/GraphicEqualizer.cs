using System.Globalization;

namespace SoundScene.Core.Audio;

/// <summary>
/// Layout of the graphic equalizer Dolby exposes through DAX.
/// </summary>
/// <remarks>
/// The DAX API returns the bands as unlabelled values, and the width is not
/// documented. The default 20-band centres come from the driver's own tuning
/// XML:
/// <c>&lt;band_20_freq fs_48000="47,141,234,...,19688"/&gt;</c> in the Realtek
/// DAX3 extension package. They are FFT-bin centres rather than round numbers,
/// which is why 1031 Hz shows up instead of 1000 Hz. Other DAX builds can
/// report a different width, so callers build their UI from
/// <see cref="CreateLayout"/> instead of assuming 20.
/// </remarks>
public static class GraphicEqualizer
{
    /// <summary>Default band count, and the width of <see cref="Frequencies"/>.</summary>
    public const int BandCount = 20;

    /// <summary>Default per-band range in 1/16 dB, matching <c>GetGEQMaximumRange</c>.</summary>
    public const int DefaultRange = 192;

    /// <summary>Center frequency of each band in Hz, low to high, at 48 kHz.</summary>
    public static IReadOnlyList<double> Frequencies { get; } =
    [
        47, 141, 234, 328, 469, 656, 844, 1031, 1313, 1688,
        2250, 3000, 3750, 4688, 5813, 7125, 9000, 11250, 13875, 19688,
    ];

    /// <summary>The layout used when the driver reports no capabilities.</summary>
    public static EqualizerLayout DefaultLayout { get; } = CreateLayout(BandCount, Frequencies);

    /// <summary>
    /// Builds a layout for a driver-reported band count.
    /// </summary>
    /// <param name="bandCount">The real count from <c>GetGEQLevels</c>.</param>
    /// <param name="frequencies">
    /// Band centres from the driver's tuning XML. When absent, the known 20-band
    /// table is used for a 20-band driver and the bands stay unlabelled
    /// otherwise - inventing centres would be worse than showing none.
    /// </param>
    public static EqualizerLayout CreateLayout(int bandCount, IReadOnlyList<double>? frequencies = null)
    {
        if (bandCount <= 0)
        {
            bandCount = BandCount;
        }

        var usable = frequencies is { Count: > 0 } && frequencies.Count == bandCount
                     && frequencies.All(value => value > 0)
            ? frequencies
            : bandCount == BandCount
                ? Frequencies
                : null;

        var bands = Enumerable.Range(0, bandCount)
            .Select(index => new EqualizerBand(index, usable?[index]))
            .ToArray();

        var groups = new List<EqualizerGroup>((bandCount + 1) / 2);
        for (var index = 0; index < bands.Length; index += 2)
        {
            var low = bands[index];
            var high = index + 1 < bands.Length ? bands[index + 1] : bands[index];
            groups.Add(new EqualizerGroup(low, high));
        }

        return new EqualizerLayout(bandCount, bands, groups, usable is not null);
    }

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
    /// The simple view's ten sliders for the default 20-band layout. Each one
    /// moves a pair of adjacent driver bands; the label is the pair's geometric
    /// centre, so it is honest about what the slider covers instead of
    /// pretending to be a 1-octave EQ.
    /// </summary>
    public static IReadOnlyList<EqualizerGroup> SimpleGroups { get; } = DefaultLayout.Groups;

    /// <summary>A flat response, used for reset and as a safe default.</summary>
    public static int[] Flat() => new int[BandCount];
}

/// <summary>One driver band. <see cref="Frequency"/> is null when unknown.</summary>
public sealed record EqualizerBand(int Index, double? Frequency)
{
    /// <summary>Axis label, e.g. <c>1.3k</c>, or <c>B7</c> when unlabelled.</summary>
    public string Label => Frequency is { } hertz
        ? GraphicEqualizer.FormatFrequency(hertz)
        : $"B{Index + 1}";
}

/// <summary>Two adjacent driver bands driven by one slider in the simple view.</summary>
public sealed record EqualizerGroup(EqualizerBand Low, EqualizerBand High)
{
    public int LowIndex => Low.Index;

    public int HighIndex => High.Index;

    /// <summary>Geometric centre, when both band centres are known.</summary>
    public double? CenterFrequency => Low.Frequency is { } low && High.Frequency is { } high
        ? Math.Sqrt(low * high)
        : null;

    public string Label => CenterFrequency is { } hertz
        ? GraphicEqualizer.FormatFrequency(hertz)
        : HighIndex == LowIndex
            ? High.Label
            : $"{Low.Label}/{High.Label}";

    /// <summary>The exact driver bands this slider writes, e.g. <c>47 Hz + 141 Hz</c>.</summary>
    public string Members => Low.Frequency is { } low && High.Frequency is { } high
        ? $"{low.ToString("0", CultureInfo.InvariantCulture)} Hz + " +
          $"{high.ToString("0", CultureInfo.InvariantCulture)} Hz"
        : HighIndex == LowIndex
            ? $"Band {LowIndex + 1}"
            : $"Bands {LowIndex + 1} + {HighIndex + 1}";
}

/// <summary>Everything the equalizer view needs to build its sliders.</summary>
public sealed record EqualizerLayout(
    int BandCount,
    IReadOnlyList<EqualizerBand> Bands,
    IReadOnlyList<EqualizerGroup> Groups,
    bool HasFrequencies);
