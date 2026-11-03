using System.Security.Cryptography;
using SoundScene.Core.Storage;

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
/// licensed on this machine. The cache is only reused while it is byte-for-byte
/// identical to that installed copy, so a stale or modified cache is replaced
/// instead of being loaded.
/// </remarks>
internal static class DaxPackageCache
{
    public static string Ensure(out string? error)
    {
        error = null;

        var dax = DaxPackages.FindDolbyAccess();
        var vclibsDirectory = DaxPackages.FindVcLibsDirectory();

        if (dax is null)
        {
            error = "Dolby Access is not installed for this user.";
            return string.Empty;
        }

        if (!dax.IsTrusted)
        {
            error =
                "The installed Dolby Access package is not trusted by Windows " +
                $"({dax.SignatureKind}{(dax.IsDevelopmentMode ? ", development mode" : string.Empty)}). " +
                "SoundScene will not load binaries from an unsigned package.";
            return string.Empty;
        }

        if (!dax.HasClientDll)
        {
            error = $"{DaxPackages.ClientDllName} is missing from the Dolby Access package.";
            return string.Empty;
        }

        if (vclibsDirectory is null)
        {
            error = "The Microsoft.VCLibs UWP runtime is not installed.";
            return string.Empty;
        }

        var cache = AppPaths.CacheDirectory;

        try
        {
            Directory.CreateDirectory(cache);

            // A junction or symlink here would let another location stand in for
            // the cache, so refuse rather than write through it.
            if (IsReparsePoint(cache))
            {
                error = $"The DAX cache folder is a reparse point: {cache}";
                return string.Empty;
            }

            var source = Path.Combine(dax.InstallPath, DaxPackages.ClientDllName);
            var target = Path.Combine(cache, DaxPackages.ClientDllName);

            if (!CopyVerified(source, target, out error))
            {
                return string.Empty;
            }

            foreach (var dll in Directory.GetFiles(vclibsDirectory, "*_app.dll"))
            {
                var destination = Path.Combine(cache, Path.GetFileName(dll));
                if (!CopyVerified(dll, destination, out error))
                {
                    return string.Empty;
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

    /// <summary>
    /// Keeps the cached copy identical to the installed one. The hash check is
    /// cheap (the client DLL is under 200 KB) and means a cache directory that
    /// was modified after the copy is never handed to the loader.
    /// </summary>
    private static bool CopyVerified(string source, string destination, out string? error)
    {
        error = null;

        var sourceHash = HashFile(source);

        if (File.Exists(destination) && !IsReparsePoint(destination))
        {
            try
            {
                if (HashFile(destination).AsSpan().SequenceEqual(sourceHash))
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Unreadable target: fall through and replace it.
            }
        }

        // Delete first so an existing symlink is removed rather than written through.
        if (File.Exists(destination))
        {
            File.Delete(destination);
        }

        File.Copy(source, destination, overwrite: false);

        if (!HashFile(destination).AsSpan().SequenceEqual(sourceHash))
        {
            error = $"The cached copy of {Path.GetFileName(source)} does not match the installed package.";
            return false;
        }

        return true;
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static byte[] HashFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return SHA256.HashData(stream);
    }
}
