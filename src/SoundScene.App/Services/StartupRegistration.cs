using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace SoundScene.App.Services;

/// <summary>
/// The per-user "start with Windows" entry, i.e. HKCU Run.
/// </summary>
/// <remarks>
/// HKCU rather than a scheduled task or a service: this is a tray app that
/// needs no elevation, and the user can remove the entry from Task Manager.
/// </remarks>
internal static class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SoundScene";

    /// <summary>
    /// Passed by the Run entry: start in the tray without opening the popup.
    /// </summary>
    public const string BackgroundArgument = "--background";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string value && value.Length > 0;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (key is null)
        {
            return;
        }

        if (enabled)
        {
            key.SetValue(ValueName, Command());
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    /// <summary>
    /// The command Windows should run. Handles both a published executable and
    /// <c>dotnet run</c>, where the process is the host and the app is a DLL.
    /// </summary>
    private static string Command()
    {
        var process = Environment.ProcessPath
                      ?? throw new InvalidOperationException("Cannot determine the executable path.");

        var isHost = string.Equals(
            Path.GetFileNameWithoutExtension(process),
            "dotnet",
            StringComparison.OrdinalIgnoreCase);

        if (isHost)
        {
            var assembly = Assembly.GetEntryAssembly()?.Location;
            if (!string.IsNullOrEmpty(assembly))
            {
                return $"\"{process}\" \"{assembly}\" {BackgroundArgument}";
            }
        }

        return $"\"{process}\" {BackgroundArgument}";
    }
}
