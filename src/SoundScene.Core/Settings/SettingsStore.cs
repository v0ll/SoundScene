using System.Text.Json;
using System.Text.Json.Serialization;
using SoundScene.Core.Hotkeys;
using SoundScene.Core.Storage;

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
        AppPaths.DataDirectory,
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
            return Deserialize(json) ?? AppSettings.CreateDefault();
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

    /// <summary>
    /// Deserialises a settings file, upgrading version 1 (toggles only) on the
    /// way in. The returned object always reports the current version, so the
    /// next save writes the new shape.
    /// </summary>
    private static AppSettings? Deserialize(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var version = root.TryGetProperty(nameof(AppSettings.Version), out var versionElement)
                      && versionElement.TryGetInt32(out var parsed)
            ? parsed
            : 0;

        // Trust the shape over the number when they disagree: a file with
        // explicit actions was written by this version even if someone dropped
        // the Version property.
        var looksCurrent = version >= AppSettings.CurrentVersion
                           || FirstMacroHasAction(root);

        if (looksCurrent)
        {
            return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
        }

        var legacy = JsonSerializer.Deserialize<LegacySettingsV1>(json, SerializerOptions);
        if (legacy is null)
        {
            return null;
        }

        return new AppSettings
        {
            Version = AppSettings.CurrentVersion,
            Macros = (legacy.Macros ?? []).Select(macro => macro.ToMacro()).ToList(),
        };
    }

    private static bool FirstMacroHasAction(JsonElement root)
    {
        if (!root.TryGetProperty(nameof(AppSettings.Macros), out var macros)
            || macros.ValueKind != JsonValueKind.Array
            || macros.GetArrayLength() == 0)
        {
            return false;
        }

        var first = macros[0];
        return first.ValueKind == JsonValueKind.Object
               && first.TryGetProperty(nameof(HotkeyMacro.Action), out _);
    }
}
