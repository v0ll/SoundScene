using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Windows.ApplicationModel;
using Windows.Management.Deployment;
using Windows.System;

namespace SoundScene.Core.Backends.Dolby;

/// <summary>The Dolby Access package that is actually installed for this user.</summary>
public sealed record DolbyAccessPackage(
    string FamilyName,
    string Version,
    string InstallPath,
    bool HasClientDll,
    PackageSignatureKind SignatureKind,
    bool IsDevelopmentMode)
{
    /// <summary>
    /// True when Windows verified the package before installing it. The files
    /// inside an MSIX carry no Authenticode signature of their own, so the
    /// package signature is the only trustworthy statement about them.
    /// </summary>
    public bool IsTrusted => SignatureKind != PackageSignatureKind.None && !IsDevelopmentMode;
}

/// <summary>The installed Dolby DAX service.</summary>
public sealed record DaxServiceInfo(string Name, string ImagePath, string? Version);

/// <summary>
/// Finds the Dolby pieces by identity instead of by an exact package name.
/// </summary>
/// <remarks>
/// The family name is stable today, but it is still a Store identity that a
/// future Dolby release or a different OEM channel could change. Matching the
/// publisher plus the presence of <c>DAXRPCClient.dll</c> is what actually makes
/// the app work, so that is what is checked.
/// </remarks>
public static class DaxPackages
{
    public const string PreferredFamilyName = "DolbyLaboratories.DolbyAccess_rz1tebttyb220";
    public const string DolbyPublisherId = "rz1tebttyb220";
    public const string DolbyPackageName = "DolbyLaboratories.DolbyAccess";
    public const string ClientDllName = "DAXRPCClient.dll";

    private const string VcLibsPrefix = "Microsoft.VCLibs.140.00_";
    private const string ServiceKey = @"SYSTEM\CurrentControlSet\Services\DolbyDAXAPI";

    public static DolbyAccessPackage? FindDolbyAccess()
    {
        var candidates = new List<DolbyAccessPackage>();

        foreach (var package in FindPackages())
        {
            if (!LooksLikeDolbyAccess(package.Id))
            {
                continue;
            }

            if (TryDescribe(package) is { } described)
            {
                candidates.Add(described);
            }
        }

        return candidates
            .OrderBy(package => package.FamilyName.Equals(PreferredFamilyName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenByDescending(package => package.Version, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    /// <summary>Directory containing the AppContainer CRT DLLs, or null.</summary>
    public static string? FindVcLibsDirectory()
    {
        var matches = new List<(ProcessorArchitecture Architecture, string Path)>();

        foreach (var package in FindPackages())
        {
            if (!package.Id.FamilyName.StartsWith(VcLibsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                var path = package.InstalledLocation?.Path;
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    matches.Add((package.Id.Architecture, path));
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or COMException)
            {
                // A broken package registration should not hide the others.
            }
        }

        return matches
            .OrderBy(match => match.Architecture == ProcessorArchitecture.X64 ? 0 : 1)
            .Select(match => match.Path)
            .FirstOrDefault();
    }

    public static DaxServiceInfo? FindService()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(ServiceKey);
            if (key is null)
            {
                return null;
            }

            var raw = key.GetValue("ImagePath") as string;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return new DaxServiceInfo("DolbyDAXAPI", string.Empty, null);
            }

            var path = ResolveImagePath(raw);
            var version = TryReadVersion(path);

            return new DaxServiceInfo("DolbyDAXAPI", path, version);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException
                                       or System.Security.SecurityException
                                       or IOException)
        {
            return null;
        }
    }

    /// <summary>Multi-line description for the diagnostics window.</summary>
    public static string Describe()
    {
        var dolby = FindDolbyAccess();
        var vclibs = FindVcLibsDirectory();
        var service = FindService();

        return string.Join(
            Environment.NewLine,
            $"Dolby Access   : {(dolby is null ? "<not found>" : $"{dolby.Version} ({dolby.FamilyName})")}",
            $"package trust  : {(dolby is null ? "<none>" : $"{dolby.SignatureKind}{(dolby.IsDevelopmentMode ? ", development mode" : string.Empty)}")}",
            $"install path   : {dolby?.InstallPath ?? "<none>"}",
            $"client dll     : {(dolby?.HasClientDll == true ? ClientDllName : "<missing>")}",
            $"VCLibs runtime : {vclibs ?? "<not found>"}",
            $"DAX service    : {(service is null ? "<not installed>" : $"{service.Name} {service.Version ?? "<unknown version>"}")}",
            $"service image  : {service?.ImagePath ?? "<none>"}");
    }

    private static IReadOnlyList<Package> FindPackages()
    {
        try
        {
            return [.. new PackageManager().FindPackagesForUser(string.Empty)];
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException
                                       or InvalidOperationException
                                       or COMException)
        {
            return [];
        }
    }

    private static bool LooksLikeDolbyAccess(PackageId id)
    {
        if (string.Equals(id.Name, DolbyPackageName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (id.FamilyName.StartsWith(DolbyPackageName + "_", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(id.PublisherId, DolbyPublisherId, StringComparison.OrdinalIgnoreCase)
               && id.Name.Contains("Dolby", StringComparison.OrdinalIgnoreCase);
    }

    private static DolbyAccessPackage? TryDescribe(Package package)
    {
        try
        {
            var path = package.InstalledLocation?.Path;
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            var version = package.Id.Version;
            return new DolbyAccessPackage(
                package.Id.FamilyName,
                $"{version.Major}.{version.Minor}.{version.Build}.{version.Revision}",
                path,
                File.Exists(Path.Combine(path, ClientDllName)),
                package.SignatureKind,
                package.IsDevelopmentMode);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or COMException)
        {
            return null;
        }
    }

    private static string ResolveImagePath(string raw)
    {
        var path = raw.Trim();

        if (path.StartsWith('"'))
        {
            var closing = path.IndexOf('"', 1);
            if (closing > 0)
            {
                path = path[1..closing];
            }
        }
        else if (path.Contains(' ') && !File.Exists(path))
        {
            // Unquoted "C:\Path With Spaces\service.exe -arg" is legal in the
            // registry; keep the whole string unless only the first token exists.
            var first = path.Split(' ')[0];
            if (File.Exists(first))
            {
                path = first;
            }
        }

        if (path.StartsWith(@"\SystemRoot\", StringComparison.OrdinalIgnoreCase))
        {
            path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                path[@"\SystemRoot\".Length..]);
        }
        else if (path.StartsWith(@"\??\", StringComparison.OrdinalIgnoreCase))
        {
            path = path[@"\??\".Length..];
        }
        else if (path.StartsWith(@"System32\", StringComparison.OrdinalIgnoreCase))
        {
            path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                path);
        }

        return Environment.ExpandEnvironmentVariables(path);
    }

    private static string? TryReadVersion(string path)
    {
        try
        {
            return File.Exists(path) ? FileVersionInfo.GetVersionInfo(path).FileVersion : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
