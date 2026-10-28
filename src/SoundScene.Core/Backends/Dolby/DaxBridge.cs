using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text;
using SoundScene.Core.Audio;

namespace SoundScene.Core.Backends.Dolby;

/// <summary>
/// Thin P/Invoke wrapper over <c>SoundScene.DaxBridge.dll</c>.
/// </summary>
/// <remarks>
/// The native session is a process-wide singleton, so this type is not
/// thread-safe by itself. <see cref="DolbyDaxBackend"/> serialises access.
/// </remarks>
internal sealed class DaxBridge : IDisposable
{
    private const string BridgeDllName = "SoundScene.DaxBridge.dll";
    private const int ReportSize = 16 * 1024;
    private const int MaxBands = 20;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int OpenFn(IntPtr cacheDir, IntPtr report, uint reportLength);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ActionFn();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int TextFn(IntPtr report, uint reportLength);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetI32Fn(out int value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SetI32Fn(int value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetGeqFn(IntPtr values, uint capacity, out uint written);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SetGeqFn(IntPtr values, uint count);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int GetStateFn(out int profile, out int subProfile, out int dolbyEnabled, out int ieq, out int maxRange);

    private readonly IntPtr _module;
    private readonly OpenFn _open;
    private readonly ActionFn _close;
    private readonly TextFn _dumpState;
    private readonly GetI32Fn _getProfile;
    private readonly SetI32Fn _setProfile;
    private readonly GetGeqFn _getGeq;
    private readonly SetGeqFn _setGeq;
    private readonly GetStateFn _getState;
    private bool _opened;

    public DaxBridge()
    {
        var path = Path.Combine(AppContext.BaseDirectory, BridgeDllName);
        if (!File.Exists(path))
        {
            throw new AudioBackendException(
                $"{BridgeDllName} is missing from the application folder. " +
                "Build the native bridge: cmake --build native/build --config Release");
        }

        _module = NativeLibrary.Load(path);
        _open = Bind<OpenFn>("ss_open");
        _close = Bind<ActionFn>("ss_close");
        _dumpState = Bind<TextFn>("ss_dump_state");
        _getProfile = Bind<GetI32Fn>("ss_get_active_profile");
        _setProfile = Bind<SetI32Fn>("ss_set_active_profile");
        _getGeq = Bind<GetGeqFn>("ss_get_geq");
        _setGeq = Bind<SetGeqFn>("ss_set_geq");
        _getState = Bind<GetStateFn>("ss_get_state");
    }

    private T Bind<T>(string name) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(_module, name));

    public string Open(string cacheDirectory)
    {
        using var dir = new AnsiString(cacheDirectory);
        var report = ReadReport((ptr, len) => _open(dir.Pointer, ptr, len), out var rc);
        if (rc != 0)
        {
            throw new AudioBackendException($"ss_open failed ({rc}): {report}");
        }

        _opened = true;
        return report ?? string.Empty;
    }

    public string DumpState()
    {
        EnsureOpen();
        return ReadReport((ptr, len) => _dumpState(ptr, len), out var rc)
               ?? throw new AudioBackendException($"ss_dump_state failed ({rc})");
    }

    public int GetProfile()
    {
        EnsureOpen();
        var rc = _getProfile(out var value);
        if (rc != 0)
        {
            throw new AudioBackendException($"ss_get_active_profile failed (0x{rc:X8})", rc);
        }

        return value;
    }

    public void SetProfile(int profile)
    {
        EnsureOpen();
        var rc = _setProfile(profile);
        if (rc != 0)
        {
            throw new AudioBackendException($"ss_set_active_profile({profile}) failed (0x{rc:X8})", rc);
        }
    }

    public AudioState GetState()
    {
        EnsureOpen();
        var rc = _getState(out var profile, out var subProfile, out var enabled, out var ieq, out var maxRange);
        if (rc != 0)
        {
            throw new AudioBackendException($"ss_get_state failed (0x{rc:X8})", rc);
        }

        return new AudioState
        {
            Profile = (AudioProfile)profile,
            SubProfile = subProfile,
            DolbyEnabled = enabled != 0,
            Ieq = ieq,
            GeqLevels = profile == (int)AudioProfile.Personalize
                ? GetGeq().ToImmutableArray()
                : ImmutableArray<int>.Empty,
            GeqMaximumRange = maxRange,
        };
    }

    public int[] GetGeq()
    {
        EnsureOpen();
        var buffer = Marshal.AllocHGlobal(MaxBands * sizeof(int));
        try
        {
            var rc = _getGeq(buffer, MaxBands, out var written);
            if (rc != 0)
            {
                throw new AudioBackendException($"ss_get_geq failed (0x{rc:X8})", rc);
            }

            var values = new int[written];
            Marshal.Copy(buffer, values, 0, (int)written);
            return values;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void SetGeq(IReadOnlyList<int> levels)
    {
        EnsureOpen();
        var buffer = Marshal.AllocHGlobal(levels.Count * sizeof(int));
        try
        {
            var values = levels.ToArray();
            Marshal.Copy(values, 0, buffer, values.Length);

            var rc = _setGeq(buffer, (uint)values.Length);
            if (rc != 0)
            {
                throw new AudioBackendException($"ss_set_geq failed (0x{rc:X8})", rc);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private void EnsureOpen()
    {
        if (!_opened)
        {
            throw new AudioBackendException("The DAX session is not open.");
        }
    }

    private static string? ReadReport(Func<IntPtr, uint, int> call, out int rc)
    {
        var buffer = Marshal.AllocHGlobal(ReportSize);
        try
        {
            rc = call(buffer, ReportSize);
            return Marshal.PtrToStringUTF8(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void Dispose()
    {
        if (_opened)
        {
            _close();
            _opened = false;
        }

        if (_module != IntPtr.Zero)
        {
            NativeLibrary.Free(_module);
        }
    }

    private sealed class AnsiString : IDisposable
    {
        public AnsiString(string value)
        {
            Pointer = Marshal.StringToHGlobalAnsi(value);
        }

        public IntPtr Pointer { get; }

        public void Dispose() => Marshal.FreeHGlobal(Pointer);
    }
}
