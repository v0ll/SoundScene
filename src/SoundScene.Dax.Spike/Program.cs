using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using SoundScene.Core.Audio;
using SoundScene.Core.Backends.Dolby;
using SoundScene.Core.Scenes;
using Windows.Management.Deployment;

namespace SoundScene.Dax.Spike;

/// <summary>
/// Feasibility probe for the Dolby DAX stack.
///
/// Modes:
///   (no args)              activation probe: RoGetActivationFactory + DllGetActivationFactory
///   load &lt;dll&gt; [extraDir]  loader-flag matrix for one DLL path
///   copytest               copy the Dolby client DLL + AppContainer CRT to %TEMP% and retry
/// </summary>
internal static class Program
{
    private const string DaxFamilyName = "DolbyLaboratories.DolbyAccess_rz1tebttyb220";
    private const string VcLibsFamilyName = "Microsoft.VCLibs.140.00_8wekyb3d8bbwe";
    private const string DaxClientDllName = "DAXRPCClient.dll";
    private const string BridgeDllName = "SoundScene.DaxBridge.dll";
    private const int ReportBufferSize = 16 * 1024;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int BridgeInfoFn(IntPtr buffer, uint length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ProbeActivationFn(IntPtr daxDllPath, IntPtr vclibsPath, IntPtr report, uint reportLength);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ProbeLoadFn(IntPtr dllPath, IntPtr extraDir, IntPtr report, uint reportLength);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int OpenFn(IntPtr cacheDir, IntPtr report, uint reportLength);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int TextFn(IntPtr report, uint reportLength);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ActionFn();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetI32Fn(out int value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SetI32Fn(int value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetGeqFn(IntPtr values, uint capacity, out uint written);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SetGeqFn(IntPtr values, uint count);

    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var bridgePath = FindBridgeDll();
        if (bridgePath is null)
        {
            Console.Error.WriteLine($"Could not locate {BridgeDllName}.");
            Console.Error.WriteLine("Build it first:  cmake --build native/build --config Release");
            return 2;
        }

        var module = NativeLibrary.Load(bridgePath);

        try
        {
            return args switch
            {
                [] => RunActivationProbe(module),
                ["load", var dll, .. var rest] => RunLoadProbe(module, dll, rest.FirstOrDefault()),
                ["activate-at", var dll, .. var rest] => RunActivationAt(module, dll, rest.FirstOrDefault()),
                ["copytest"] => RunCopyTest(module),
                ["open"] => RunOpen(module),
                ["writetest"] => RunWriteTest(module),
                ["geq", var profile] => RunGeqTest(module, int.Parse(profile, CultureInfo.InvariantCulture)),
                ["core"] => RunCoreProbe().GetAwaiter().GetResult(),
                ["scenes"] => RunSceneProbe().GetAwaiter().GetResult(),
                _ => Usage(),
            };
        }
        finally
        {
            NativeLibrary.Free(module);
        }
    }

    private static int Usage()
    {
        Console.WriteLine("usage:");
        Console.WriteLine("  SoundScene.Dax.Spike            activation probe");
        Console.WriteLine("  SoundScene.Dax.Spike load <dll> [extraDir]");
        Console.WriteLine("  SoundScene.Dax.Spike copytest");
        return 1;
    }

    private static int RunActivationProbe(IntPtr module)
    {
        Console.WriteLine("SoundScene - DAX activation probe");
        Console.WriteLine("================================");
        Console.WriteLine();

        var daxPath = FindPackageFile(DaxFamilyName, DaxClientDllName);
        var vclibsDir = FindPackageDirectory(VcLibsFamilyName);

        Console.WriteLine($"dax client dll : {daxPath ?? "<not found>"}");
        Console.WriteLine($"vclibs dir     : {vclibsDir ?? "<not found>"}");
        Console.WriteLine();

        if (daxPath is null || vclibsDir is null)
        {
            Console.Error.WriteLine("Dolby Access or the VC++ UWP runtime is not installed for this user.");
            return 3;
        }

        var info = GetExport<BridgeInfoFn>(module, "ss_bridge_info");
        var probe = GetExport<ProbeActivationFn>(module, "ss_probe_activation");

        Console.WriteLine($"bridge reports : {InvokeText(info)}");
        Console.WriteLine();

        return InvokeReport((report, len) =>
        {
            using var dax = new AnsiString(daxPath);
            using var vcl = new AnsiString(vclibsDir);
            return probe(dax.Pointer, vcl.Pointer, report, len);
        });
    }

    private static async Task<int> RunCoreProbe()
    {
        Console.WriteLine("SoundScene - Core backend probe");
        Console.WriteLine("==============================");
        Console.WriteLine();

        await using var backend = new DolbyDaxBackend();

        var state = await backend.GetStateAsync();
        Console.WriteLine($"backend        : {backend.Name}");
        Console.WriteLine($"profile        : {state.Profile} ({state.Profile.DisplayName()})");
        Console.WriteLine($"dolby enabled  : {state.DolbyEnabled}");
        Console.WriteLine($"sub profile    : {state.SubProfile}");
        Console.WriteLine($"ieq            : {state.Ieq}");
        Console.WriteLine($"eq max range   : {state.GeqMaximumRange}");
        Console.WriteLine($"eq bands       : [{string.Join(", ", state.GeqLevels)}]");
        Console.WriteLine();

        var original = state.Profile;
        var target = original == AudioProfile.Movie ? AudioProfile.Game : AudioProfile.Movie;

        Console.WriteLine($"switching      : {original.DisplayName()} -> {target.DisplayName()}");
        await backend.SetProfileAsync(target);
        await Task.Delay(250);

        var after = await backend.GetStateAsync();
        Console.WriteLine($"read back      : {after.Profile.DisplayName()}  {(after.Profile == target ? "OK" : "MISMATCH")}");

        await backend.SetProfileAsync(original);
        await Task.Delay(250);
        var restored = await backend.GetStateAsync();
        Console.WriteLine($"restored       : {restored.Profile.DisplayName()}  {(restored.Profile == original ? "OK" : "MISMATCH")}");
        Console.WriteLine();

        Console.WriteLine("writing eq bands through the abstraction...");
        var levels = (await backend.GetGeqAsync()).ToArray();
        if (levels.Length == 20)
        {
            var changed = (int[])levels.Clone();
            changed[0] = changed[0] == 0 ? 40 : 0;

            await backend.SetGeqAsync(changed);
            await Task.Delay(250);
            var afterEq = (await backend.GetGeqAsync()).ToArray();
            Console.WriteLine($"band 0         : {levels[0]} -> {afterEq[0]}  {(afterEq[0] == changed[0] ? "OK" : "MISMATCH")}");

            await backend.SetGeqAsync(levels);
            await Task.Delay(250);
            var finalEq = (await backend.GetGeqAsync()).ToArray();
            var finalState = await backend.GetStateAsync();
            Console.WriteLine($"restored band 0: {finalEq[0]}  {(finalEq[0] == levels[0] ? "OK" : "MISMATCH")}");
            Console.WriteLine($"profile after  : {finalState.Profile.DisplayName()}  {(finalState.Profile == original ? "OK" : "MISMATCH")}");
        }
        else
        {
            Console.WriteLine($"unexpected band count: {levels.Length}");
        }

        return 0;
    }

    private static async Task<int> RunSceneProbe()
    {
        Console.WriteLine("SoundScene - scene library probe");
        Console.WriteLine("================================");
        Console.WriteLine();

        var path = Path.Combine(Path.GetTempPath(), $"soundscene-scene-probe-{Guid.NewGuid():N}.json");

        await using var backend = new DolbyDaxBackend();
        var library = new SceneLibrary(backend, new SceneStore(path));

        try
        {
            var start = await backend.GetStateAsync();
            var originalEq = (await backend.GetGeqAsync()).ToArray();
            Console.WriteLine($"start          : {start.Profile.DisplayName()}, band 0 = {originalEq[0]}");
            Console.WriteLine();

            await backend.SetProfileAsync(AudioProfile.Movie);
            var movieScene = await library.CaptureAsync("probe-movie");
            Console.WriteLine($"capture movie  : {movieScene.Summary}, custom eq = {movieScene.HasCustomEq}");

            await backend.SetProfileAsync(AudioProfile.Personalize);
            var changed = (int[])originalEq.Clone();
            changed[0] = changed[0] == 0 ? 40 : 0;
            await backend.SetGeqAsync(changed);

            var eqScene = await library.CaptureAsync("probe-eq");
            Console.WriteLine($"capture eq     : {eqScene.Summary}, band 0 = {eqScene.GeqLevels[0]}");

            var reloaded = new SceneLibrary(backend, new SceneStore(path));
            reloaded.Load();
            Console.WriteLine($"reload         : [{string.Join(", ", reloaded.Scenes.Select(s => s.Name))}]");

            await backend.SetProfileAsync(AudioProfile.Movie);
            await backend.SetGeqAsync(originalEq);
            Console.WriteLine();

            await reloaded.ApplyAsync(reloaded.Scenes.Single(s => s.Name == "probe-eq"));
            var afterEq = await backend.GetStateAsync();
            var levels = await backend.GetGeqAsync();
            var eqOk = levels[0] == changed[0] && afterEq.Profile == AudioProfile.Personalize;
            Console.WriteLine($"apply eq       : profile = {afterEq.Profile.DisplayName()}, band 0 = {levels[0]} (expected {changed[0]}) {(eqOk ? "OK" : "MISMATCH")}");

            await reloaded.ApplyAsync(reloaded.Scenes.Single(s => s.Name == "probe-movie"));
            var afterMovie = await backend.GetStateAsync();
            var movieOk = afterMovie.Profile == AudioProfile.Movie;
            Console.WriteLine($"apply movie    : profile = {afterMovie.Profile.DisplayName()} {(movieOk ? "OK" : "MISMATCH")}");

            var removed = reloaded.Remove("probe-movie") && reloaded.Remove("probe-eq");
            var finalReload = new SceneLibrary(backend, new SceneStore(path));
            finalReload.Load();
            Console.WriteLine($"remove         : removed = {removed}, remaining = {finalReload.Scenes.Count}");
            Console.WriteLine();

            await backend.SetGeqAsync(originalEq);
            await backend.SetProfileAsync(start.Profile);
            var restored = await backend.GetStateAsync();
            var restoredEq = await backend.GetGeqAsync();
            Console.WriteLine($"restore        : profile = {restored.Profile.DisplayName()}, band 0 = {restoredEq[0]}");

            var ok = eqOk && movieOk && removed && finalReload.Scenes.Count == 0 &&
                     restored.Profile == start.Profile && restoredEq[0] == originalEq[0];

            Console.WriteLine(ok ? "RESULT         : PASS" : "RESULT         : FAIL");
            return ok ? 0 : 7;
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static int RunGeqTest(IntPtr module, int profileIndex)
    {
        Console.WriteLine($"SoundScene - GEQ write test under profile {profileIndex}");
        Console.WriteLine("=================================================");
        Console.WriteLine();

        var cache = PrepareCache();
        if (cache is null)
        {
            return 3;
        }

        var open = GetExport<OpenFn>(module, "ss_open");
        var close = GetExport<ActionFn>(module, "ss_close");
        var getProfile = GetExport<GetI32Fn>(module, "ss_get_active_profile");
        var setProfile = GetExport<SetI32Fn>(module, "ss_set_active_profile");
        var getGeq = GetExport<GetGeqFn>(module, "ss_get_geq");
        var setGeq = GetExport<SetGeqFn>(module, "ss_set_geq");

        try
        {
            var rc = InvokeReport((report, len) =>
            {
                using var dir = new AnsiString(cache);
                return open(dir.Pointer, report, len);
            });

            if (rc != 0)
            {
                return rc;
            }

            getProfile(out var originalProfile);
            Console.WriteLine($"original profile : {originalProfile}");

            if (originalProfile != profileIndex)
            {
                Console.WriteLine($"switching to     : {profileIndex}");
                setProfile(profileIndex);
                Thread.Sleep(500);
                getProfile(out var now);
                Console.WriteLine($"now profile      : {now}");
            }

            var buffer = Marshal.AllocHGlobal(20 * sizeof(int));
            try
            {
                if (getGeq(buffer, 20, out var written) != 0 || written == 0)
                {
                    Console.Error.WriteLine("ss_get_geq failed");
                    return 6;
                }

                var original = new int[written];
                Marshal.Copy(buffer, original, 0, (int)written);
                Console.WriteLine($"geq before       : [{string.Join(", ", original)}]");

                var changed = (int[])original.Clone();
                changed[0] = original[0] == 0 ? 48 : 0;
                Marshal.Copy(changed, 0, buffer, changed.Length);

                var hr = setGeq(buffer, written);
                Console.WriteLine($"set band 0 to    : {changed[0]}  (hr=0x{hr:X8})");

                Thread.Sleep(400);
                getGeq(buffer, 20, out _);
                var after = new int[written];
                Marshal.Copy(buffer, after, 0, (int)written);
                Console.WriteLine($"geq after        : [{string.Join(", ", after)}]");
                Console.WriteLine($"result           : {(after[0] == changed[0] ? "WRITE OK" : "WRITE REJECTED")}");

                Marshal.Copy(original, 0, buffer, original.Length);
                setGeq(buffer, written);
                Thread.Sleep(300);
                getGeq(buffer, 20, out _);
                var restored = new int[written];
                Marshal.Copy(buffer, restored, 0, (int)written);
                Console.WriteLine($"restored         : [{string.Join(", ", restored)}]");
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            if (originalProfile != profileIndex)
            {
                setProfile(originalProfile);
                Console.WriteLine($"profile restored : {originalProfile}");
            }

            return 0;
        }
        finally
        {
            close();
        }
    }

    private static int RunWriteTest(IntPtr module)
    {
        Console.WriteLine("SoundScene - DAX write test");
        Console.WriteLine("===========================");
        Console.WriteLine();
        Console.WriteLine("Changes one profile and one EQ band, verifies, then restores.");
        Console.WriteLine();

        var cache = PrepareCache();
        if (cache is null)
        {
            return 3;
        }

        var open = GetExport<OpenFn>(module, "ss_open");
        var close = GetExport<ActionFn>(module, "ss_close");
        var getProfile = GetExport<GetI32Fn>(module, "ss_get_active_profile");
        var setProfile = GetExport<SetI32Fn>(module, "ss_set_active_profile");
        var getGeq = GetExport<GetGeqFn>(module, "ss_get_geq");
        var setGeq = GetExport<SetGeqFn>(module, "ss_set_geq");

        try
        {
            var rc = InvokeReport((report, len) =>
            {
                using var dir = new AnsiString(cache);
                return open(dir.Pointer, report, len);
            });

            if (rc != 0)
            {
                return rc;
            }

            // ---- profile round trip
            Console.WriteLine("--- profile ---");
            if (getProfile(out var originalProfile) != 0)
            {
                Console.Error.WriteLine("ss_get_active_profile failed");
                return 5;
            }

            Console.WriteLine($"original       : {originalProfile}");
            var target = originalProfile == 0 ? 1 : 0;
            Console.WriteLine($"setting        : {target}");

            var setHr = setProfile(target);
            Console.WriteLine($"set hr         : 0x{setHr:X8}");

            Thread.Sleep(300);
            getProfile(out var afterProfile);
            Console.WriteLine($"read back      : {afterProfile}   {(afterProfile == target ? "CHANGED" : "UNCHANGED")}");

            Console.WriteLine($"restoring      : {originalProfile}");
            setProfile(originalProfile);
            Thread.Sleep(300);
            getProfile(out var restoredProfile);
            Console.WriteLine($"read back      : {restoredProfile}   {(restoredProfile == originalProfile ? "RESTORED" : "MISMATCH")}");
            Console.WriteLine();

            // ---- geq round trip
            Console.WriteLine("--- geq ---");
            var buffer = Marshal.AllocHGlobal(20 * sizeof(int));
            try
            {
                if (getGeq(buffer, 20, out var written) != 0 || written == 0)
                {
                    Console.Error.WriteLine("ss_get_geq failed");
                    return 6;
                }

                var values = new int[written];
                Marshal.Copy(buffer, values, 0, (int)written);
                Console.WriteLine($"original       : [{string.Join(", ", values)}]");

                var probeValues = (int[])values.Clone();
                probeValues[0] = probeValues[0] == 0 ? 48 : 0;
                Console.WriteLine($"setting band 0 : {probeValues[0]}");

                Marshal.Copy(probeValues, 0, buffer, probeValues.Length);
                var geqHr = setGeq(buffer, written);
                Console.WriteLine($"set hr         : 0x{geqHr:X8}");

                Thread.Sleep(300);
                getGeq(buffer, 20, out _);
                Marshal.Copy(buffer, probeValues, 0, (int)written);
                Console.WriteLine($"read back      : [{string.Join(", ", probeValues)}]");
                Console.WriteLine($"band 0         : {(probeValues[0] == (values[0] == 0 ? 48 : 0) ? "CHANGED" : "UNCHANGED")}");

                Console.WriteLine("restoring      : original levels");
                Marshal.Copy(values, 0, buffer, values.Length);
                setGeq(buffer, written);
                Thread.Sleep(300);
                getGeq(buffer, 20, out _);
                Marshal.Copy(buffer, probeValues, 0, (int)written);
                Console.WriteLine($"read back      : [{string.Join(", ", probeValues)}]");
                Console.WriteLine($"band 0         : {(probeValues[0] == values[0] ? "RESTORED" : "MISMATCH")}");
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }

            return 0;
        }
        finally
        {
            close();
        }
    }

    private static int RunOpen(IntPtr module)
    {
        Console.WriteLine("SoundScene - DAX session");
        Console.WriteLine("========================");
        Console.WriteLine();

        var cache = PrepareCache();
        if (cache is null)
        {
            return 3;
        }

        Console.WriteLine($"cache          : {cache}");
        Console.WriteLine();

        var open = GetExport<OpenFn>(module, "ss_open");
        var dump = GetExport<TextFn>(module, "ss_dump_state");
        var close = GetExport<ActionFn>(module, "ss_close");

        try
        {
            var rc = InvokeReport((report, len) =>
            {
                using var dir = new AnsiString(cache);
                return open(dir.Pointer, report, len);
            });

            if (rc != 0)
            {
                return rc;
            }

            Console.WriteLine();
            Console.WriteLine("--- state ---");
            return InvokeReport((report, len) => dump(report, len));
        }
        finally
        {
            close();
        }
    }

    /// <summary>
    /// Copies the Dolby client DLL and the AppContainer CRT into a private cache
    /// directory. Windows refuses to execute code from WindowsApps for unpackaged
    /// processes, so the copy is mandatory, not an optimisation.
    /// </summary>
    private static string? PrepareCache()
    {
        var daxDir = FindPackageDirectory(DaxFamilyName);
        var vclibsDir = FindPackageDirectory(VcLibsFamilyName);

        if (daxDir is null || vclibsDir is null)
        {
            Console.Error.WriteLine("Dolby Access or the VC++ UWP runtime is not installed for this user.");
            return null;
        }

        var cache = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SoundScene",
            "dax-cache");

        Directory.CreateDirectory(cache);

        File.Copy(Path.Combine(daxDir, DaxClientDllName), Path.Combine(cache, DaxClientDllName), overwrite: true);

        foreach (var dll in Directory.GetFiles(vclibsDir, "*_app.dll"))
        {
            File.Copy(dll, Path.Combine(cache, Path.GetFileName(dll)), overwrite: true);
        }

        return cache;
    }

    private static int RunActivationAt(IntPtr module, string dllPath, string? extraDir)
    {
        Console.WriteLine("SoundScene - activation probe at explicit path");
        Console.WriteLine("==============================================");
        Console.WriteLine();

        var probe = GetExport<ProbeActivationFn>(module, "ss_probe_activation");
        return InvokeReport((report, len) =>
        {
            using var path = new AnsiString(dllPath);
            using var extra = new AnsiString(extraDir);
            return probe(path.Pointer, extra.Pointer, report, len);
        });
    }

    private static int RunLoadProbe(IntPtr module, string dllPath, string? extraDir)
    {
        Console.WriteLine("SoundScene - loader probe");
        Console.WriteLine("=========================");
        Console.WriteLine();

        var probe = GetExport<ProbeLoadFn>(module, "ss_probe_load");
        return InvokeReport((report, len) =>
        {
            using var path = new AnsiString(dllPath);
            using var extra = new AnsiString(extraDir);
            return probe(path.Pointer, extra.Pointer, report, len);
        });
    }

    private static int RunCopyTest(IntPtr module)
    {
        var daxDir = FindPackageDirectory(DaxFamilyName);
        var vclibsDir = FindPackageDirectory(VcLibsFamilyName);

        if (daxDir is null || vclibsDir is null)
        {
            Console.Error.WriteLine("Package not found.");
            return 3;
        }

        var temp = Path.Combine(Path.GetTempPath(), "soundscene-dax-copytest");
        if (Directory.Exists(temp))
        {
            Directory.Delete(temp, recursive: true);
        }
        Directory.CreateDirectory(temp);

        var source = Path.Combine(daxDir, DaxClientDllName);
        var copied = Path.Combine(temp, DaxClientDllName);
        File.Copy(source, copied);

        foreach (var dll in Directory.GetFiles(vclibsDir, "*_app.dll"))
        {
            File.Copy(dll, Path.Combine(temp, Path.GetFileName(dll)));
        }

        Console.WriteLine("SoundScene - copy test");
        Console.WriteLine("======================");
        Console.WriteLine($"copied to      : {temp}");
        Console.WriteLine();
        Console.WriteLine("This is a diagnostic only. A copied vendor DLL is never shipped.");
        Console.WriteLine();

        var probe = GetExport<ProbeLoadFn>(module, "ss_probe_load");

        Console.WriteLine("--- in place (WindowsApps) ---");
        var rc = InvokeReport((report, len) =>
        {
            using var path = new AnsiString(source);
            using var extra = new AnsiString(vclibsDir);
            return probe(path.Pointer, extra.Pointer, report, len);
        });
        if (rc != 0)
        {
            return rc;
        }

        Console.WriteLine();
        Console.WriteLine("--- copied to %TEMP% ---");
        return InvokeReport((report, len) =>
        {
            using var path = new AnsiString(copied);
            using var extra = new AnsiString(temp);
            return probe(path.Pointer, extra.Pointer, report, len);
        });
    }

    private static int InvokeReport(Func<IntPtr, uint, int> call)
    {
        var report = Marshal.AllocHGlobal(ReportBufferSize);
        try
        {
            var rc = call(report, ReportBufferSize);
            if (rc != 0)
            {
                Console.Error.WriteLine($"native call failed with {rc}.");
                return 4;
            }

            Console.WriteLine(Marshal.PtrToStringUTF8(report));
            return 0;
        }
        finally
        {
            Marshal.FreeHGlobal(report);
        }
    }

    private static T GetExport<T>(IntPtr module, string name) where T : Delegate
    {
        var address = NativeLibrary.GetExport(module, name);
        return Marshal.GetDelegateForFunctionPointer<T>(address);
    }

    private static string InvokeText(BridgeInfoFn fn)
    {
        var ptr = Marshal.AllocHGlobal(1024);
        try
        {
            return fn(ptr, 1024) == 0 ? Marshal.PtrToStringUTF8(ptr) ?? "<null>" : "<failed>";
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private static string? FindPackageDirectory(string familyName)
    {
        var package = new PackageManager()
            .FindPackagesForUser(string.Empty)
            .FirstOrDefault(p => string.Equals(p.Id.FamilyName, familyName, StringComparison.OrdinalIgnoreCase));

        return package?.InstalledLocation?.Path;
    }

    private static string? FindPackageFile(string familyName, string fileName)
    {
        var dir = FindPackageDirectory(familyName);
        if (dir is null)
        {
            return null;
        }

        var candidate = Path.Combine(dir, fileName);
        return File.Exists(candidate) ? candidate : null;
    }

    /// <summary>
    /// Walks up from the build output directory looking for the repository root,
    /// then locates the native bridge under native/build.
    /// </summary>
    private static string? FindBridgeDll()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "SoundScene.slnx")))
            {
                foreach (var config in new[] { "Release", "Debug" })
                {
                    var candidate = Path.Combine(dir.FullName, "native", "build", config, BridgeDllName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }

            dir = dir.Parent;
        }

        return null;
    }

    /// <summary>Null-tolerant ANSI string allocated on the unmanaged heap.</summary>
    private sealed class AnsiString : IDisposable
    {
        public AnsiString(string? value)
        {
            Pointer = value is null ? IntPtr.Zero : Marshal.StringToHGlobalAnsi(value);
        }

        public IntPtr Pointer { get; }

        public void Dispose()
        {
            if (Pointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(Pointer);
            }
        }
    }
}
