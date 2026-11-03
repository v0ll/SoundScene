using System.Runtime.InteropServices;

namespace SoundScene.Core.Backends.Dolby;

/// <summary>One thing SoundScene needs before it can talk to Dolby.</summary>
public sealed record DaxRequirement(string Name, bool Ok, string Detail);

/// <summary>The result of checking the local Dolby installation.</summary>
public sealed record DaxRequirements(IReadOnlyList<DaxRequirement> Items)
{
    public bool IsReady => Items.All(item => item.Ok);

    /// <summary>Short phrase for a status line or tooltip.</summary>
    public string Summary
    {
        get
        {
            if (IsReady)
            {
                return "Dolby is ready";
            }

            var missing = Items.FirstOrDefault(item => !item.Ok);
            return missing is null ? "Setup required" : $"{missing.Name} is missing";
        }
    }

    /// <summary>Multi-line report for the diagnostics window.</summary>
    public string ToText() =>
        string.Join(
            Environment.NewLine,
            Items.Select(item => $"[{(item.Ok ? "ok" : "!!")}] {item.Name} - {item.Detail}"));
}

/// <summary>
/// Checks the pieces SoundScene needs, without changing anything.
/// </summary>
/// <remarks>
/// The ROG driver installs the DAX service and the APOs, but the supported
/// control surface (<c>DAXRPCClient.dll</c> / <c>DAX.winmd</c>) ships in the
/// Dolby Access package. That is why the app can launch with only the driver
/// but cannot switch profiles - the check makes the missing package explicit
/// instead of letting a raw HRESULT be the first thing a user sees.
/// </remarks>
public static class DaxRequirementChecker
{
    private const string BridgeDllName = "SoundScene.DaxBridge.dll";

    public static DaxRequirements Check()
    {
        var items = new List<DaxRequirement>(6);

        var windowsOk = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041);
        var windowsVersion = Environment.OSVersion.Version;
        var windowsName = windowsVersion.Build >= 22000 ? "Windows 11" : "Windows 10";
        items.Add(new DaxRequirement(
            "Windows 10 2004 or newer",
            windowsOk,
            windowsOk ? $"{windowsName} (build {windowsVersion.Build})" : "This Windows version is too old."));

        var x64 = RuntimeInformation.ProcessArchitecture == Architecture.X64;
        items.Add(new DaxRequirement(
            "64-bit Windows",
            x64,
            x64 ? "x64" : $"{RuntimeInformation.ProcessArchitecture} is not supported."));

        var dolby = DaxPackages.FindDolbyAccess();
        var dolbyOk = dolby is { HasClientDll: true };
        items.Add(new DaxRequirement(
            "Dolby Access",
            dolbyOk,
            dolby is null
                ? "Not installed. Get it free from the Microsoft Store."
                : dolby.HasClientDll
                    ? $"Version {dolby.Version} ({dolby.FamilyName})"
                    : $"Version {dolby.Version} is missing {DaxPackages.ClientDllName}."));

        var vclibs = DaxPackages.FindVcLibsDirectory() is not null;
        items.Add(new DaxRequirement(
            "Microsoft VCLibs runtime",
            vclibs,
            vclibs ? "Present" : "Installed with Dolby Access."));

        var service = DaxPackages.FindService();
        var serviceOk = service is not null;
        items.Add(new DaxRequirement(
            "Dolby DAX service",
            serviceOk,
            service is null
                ? "Install the ASUS Dolby Atmos driver for this laptop."
                : service.Version is null
                    ? "DolbyDAXAPI is installed."
                    : $"DolbyDAXAPI {service.Version} is installed."));

        var bridgePath = Path.Combine(AppContext.BaseDirectory, BridgeDllName);
        var bridgeOk = File.Exists(bridgePath);
        items.Add(new DaxRequirement(
            "SoundScene DAX bridge",
            bridgeOk,
            bridgeOk ? BridgeDllName : "Missing from the application folder. Reinstall SoundScene."));

        return new DaxRequirements(items);
    }
}
