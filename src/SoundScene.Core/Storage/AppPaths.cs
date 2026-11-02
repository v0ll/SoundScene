namespace SoundScene.Core.Storage;

/// <summary>
/// Where the app keeps its own files.
/// </summary>
/// <remarks>
/// A normal install keeps settings and scenes in <c>%APPDATA%\SoundScene</c>
/// and the copied Dolby client in <c>%LOCALAPPDATA%\SoundScene</c>. A portable
/// copy keeps both under <c>data</c> next to the executable, so deleting that
/// folder removes every trace of the app.
/// </remarks>
public static class AppPaths
{
    private static string? _dataDirectory;
    private static string? _cacheDirectory;

    public static bool IsPortable { get; private set; }

    /// <summary>Settings and scenes.</summary>
    public static string DataDirectory => _dataDirectory ??=
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SoundScene");

    /// <summary>The private copy of Dolby's client DLL and AppContainer CRT.</summary>
    public static string CacheDirectory => _cacheDirectory ??=
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SoundScene",
            "dax-cache");

    /// <summary>Switches to <c>&lt;baseDirectory&gt;\data</c>. Call once at startup.</summary>
    public static void UsePortable(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);

        IsPortable = true;
        _dataDirectory = Path.Combine(baseDirectory, "data");
        _cacheDirectory = Path.Combine(_dataDirectory, "dax-cache");
    }
}
