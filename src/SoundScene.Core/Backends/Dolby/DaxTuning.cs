using System.Globalization;
using System.Text.RegularExpressions;

namespace SoundScene.Core.Backends.Dolby;

/// <summary>Band centres and the file they came from.</summary>
public sealed record DaxTuningBands(IReadOnlyList<double> Frequencies, string Source);

/// <summary>
/// Reads the equalizer band centres out of the installed DAX tuning XML.
/// </summary>
/// <remarks>
/// Dolby's API exposes band values but not their frequencies. The frequencies
/// live next to the driver in
/// <c>System32\DriverStore\FileRepository\dax3_ext_*\*.xml</c> as
/// <c>&lt;band_20_freq fs_48000="47,141,..."/&gt;</c>. That file is per model,
/// which is exactly why hardcoding the G14's table was not good enough.
///
/// Failure is ordinary: the directory may not be readable, the build may use a
/// different schema, or the machine may have no DAX install at all. Callers
/// fall back to unlabelled bands.
/// </remarks>
public static class DaxTuning
{
    private static readonly object Gate = new();
    private static readonly Dictionary<int, DaxTuningBands?> Cache = [];

    public static DaxTuningBands? TryRead(int bandCount)
    {
        if (bandCount <= 0)
        {
            return null;
        }

        lock (Gate)
        {
            if (Cache.TryGetValue(bandCount, out var cached))
            {
                return cached;
            }

            var result = Read(bandCount);
            Cache[bandCount] = result;
            return result;
        }
    }

    /// <summary>Parses one XML document. Public so the format can be tested.</summary>
    public static IReadOnlyList<double>? ParseBandFrequencies(string xml, int bandCount)
    {
        if (string.IsNullOrEmpty(xml) || bandCount <= 0)
        {
            return null;
        }

        var tag = Regex.Match(
            xml,
            $@"<band_{bandCount}_freq\b[^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (!tag.Success)
        {
            return null;
        }

        foreach (var attribute in new[] { "fs_48000", "fs_44100" })
        {
            var value = Regex.Match(
                tag.Value,
                $@"\b{attribute}\s*=\s*""([^""]*)""",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            if (!value.Success)
            {
                continue;
            }

            var parts = value.Groups[1].Value.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length != bandCount)
            {
                continue;
            }

            var frequencies = new double[bandCount];
            var ok = true;

            for (var index = 0; index < parts.Length; index++)
            {
                if (!double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var hertz)
                    || hertz <= 0)
                {
                    ok = false;
                    break;
                }

                frequencies[index] = hertz;
            }

            if (ok)
            {
                return frequencies;
            }
        }

        return null;
    }

    private static DaxTuningBands? Read(int bandCount)
    {
        try
        {
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var repository = Path.Combine(windows, "System32", "DriverStore", "FileRepository");

            if (!Directory.Exists(repository))
            {
                return null;
            }

            // Device tuning first; the SWC packages hold service defaults.
            var directories = Directory
                .EnumerateDirectories(repository, "dax3_*")
                .OrderBy(path => Path.GetFileName(path).StartsWith("dax3_ext_", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase);

            foreach (var directory in directories)
            {
                IEnumerable<string> files;
                try
                {
                    files = Directory
                        .EnumerateFiles(directory, "*.xml")
                        .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                foreach (var file in files)
                {
                    string xml;
                    try
                    {
                        xml = File.ReadAllText(file);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        continue;
                    }

                    var frequencies = ParseBandFrequencies(xml, bandCount);
                    if (frequencies is not null)
                    {
                        return new DaxTuningBands(frequencies, file);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Tuning is optional; unlabelled bands are fine.
        }

        return null;
    }
}
