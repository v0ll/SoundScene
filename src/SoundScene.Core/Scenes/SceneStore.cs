using System.Text.Json;
using System.Text.Json.Serialization;

namespace SoundScene.Core.Scenes;

/// <summary>On-disk shape of the scene library. Versioned for future migration.</summary>
public sealed class SceneFile
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public List<Scene> Scenes { get; set; } = [];
}

/// <summary>Persists scenes as JSON, next to the settings file.</summary>
public sealed class SceneStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public SceneStore(string? filePath = null)
    {
        FilePath = filePath ?? DefaultFilePath;
    }

    public string FilePath { get; }

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "SoundScene",
        "scenes.json");

    public IReadOnlyList<Scene> Load()
    {
        if (!File.Exists(FilePath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            var file = JsonSerializer.Deserialize<SceneFile>(json, SerializerOptions);
            return file?.Scenes ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    public void Save(IEnumerable<Scene> scenes)
    {
        ArgumentNullException.ThrowIfNull(scenes);

        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var file = new SceneFile { Scenes = [.. scenes] };
        File.WriteAllText(FilePath, JsonSerializer.Serialize(file, SerializerOptions));
    }
}
