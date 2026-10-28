using Windows.Management.Deployment;

namespace SoundScene.Core.Backends.Dolby;

/// <summary>
/// Prepares a private, loadable copy of Dolby's client DLL.
/// </summary>
/// <remarks>
/// Windows refuses to map executable images out of
/// <c>C:\Program Files\WindowsApps</c> into a process without package identity
/// (see docs/adr/0002). Copying the client DLL and the AppContainer CRT into
/// %LOCALAPPDATA% is therefore mandatory, not an optimisation.
///
/// Nothing is redistributed: the bytes come from the copy already installed and
/// licensed on this machine. The cache is refreshed whenever the package changes.
/// </remarks>
internal static class DaxPackageCache
{
    private const string DaxFamilyName = "DolbyLaboratories.DolbyAccess_rz1tebttyb220";
    private const string VcLibsFamilyName = "Microsoft.VCLibs.140.00_8wekyb3d8bbwe";
    private const string DaxClientDllName = "DAXRPCClient.dll";

    public static string Ensure(out string? error)
    {
        error = null;

        var daxDirectory = FindPackageDirectory(DaxFamilyName);
        var vclibsDirectory = FindPackageDirectory(VcLibsFamilyName);

        if (daxDirectory is null)
        {
            error = "Dolby Access is not installed for this user.";
            return string.Empty;
        }

        if (vclibsDirectory is null)
        {
            error = "The Microsoft.VCLibs UWP runtime is not installed.";
            return string.Empty;
        }

        var cache = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SoundScene",
            "dax-cache");

        try
        {
            Directory.CreateDirectory(cache);

            var source = Path.Combine(daxDirectory, DaxClientDllName);
            var target = Path.Combine(cache, DaxClientDllName);

            if (NeedsCopy(source, target))
            {
                File.Copy(source, target, overwrite: true);
            }

            foreach (var dll in Directory.GetFiles(vclibsDirectory, "*_app.dll"))
            {
                var destination = Path.Combine(cache, Path.GetFileName(dll));
                if (NeedsCopy(dll, destination))
                {
                    File.Copy(dll, destination, overwrite: true);
                }
            }

            return cache;
        }
        catch (IOException ex)
        {
            error = $"Could not prepare the DAX cache: {ex.Message}";
            return string.Empty;
        }
        catch (UnauthorizedAccessException ex)
        {
            error = $"Access denied preparing the DAX cache: {ex.Message}";
            return string.Empty;
        }
    }

    private static bool NeedsCopy(string source, string target)
    {
        var sourceInfo = new FileInfo(source);
        if (!sourceInfo.Exists)
        {
            return false;
        }

        var targetInfo = new FileInfo(target);
        return !targetInfo.Exists
               || targetInfo.Length != sourceInfo.Length
               || targetInfo.LastWriteTimeUtc != sourceInfo.LastWriteTimeUtc;
    }

    private static string? FindPackageDirectory(string familyName)
    {
        var package = new PackageManager()
            .FindPackagesForUser(string.Empty)
            .FirstOrDefault(p => string.Equals(p.Id.FamilyName, familyName, StringComparison.OrdinalIgnoreCase));

        return package?.InstalledLocation?.Path;
    }
}
