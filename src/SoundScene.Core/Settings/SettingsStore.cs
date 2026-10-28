using System.Text.Json;
using System.Text.Json.Serialization;

namespace SoundScene.Core.Settings;

/// <summary>Loads and saves <see cref="AppSettings"/> as JSON.</summary>
/// <remarks>
/// A corrupt or unreadable file is never fatal: the store falls back to defaults
/// so the app always starts. Losing hotkey customisation is better than refusing
/// to launch.
/// </remarks>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public SettingsStore(string? filePath = null)
    {
        FilePath = filePath ?? DefaultFilePath;
    }

    public string FilePath { get; }

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SoundScene",
        "settings.json");

    public AppSettings Load()
    {
        if (!File.Exists(FilePath))
        {
            return AppSettings.CreateDefault();
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions)
                   ?? AppSettings.CreateDefault();
        }
        catch (JsonException)
        {
            return AppSettings.CreateDefault();
        }
        catch (IOException)
        {
            return AppSettings.CreateDefault();
        }
        catch (UnauthorizedAccessException)
        {
            return AppSettings.CreateDefault();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, SerializerOptions));
    }
}
