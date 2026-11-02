using System.Runtime;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace SoundScene.App.Services;

/// <summary>
/// Returns unused memory to the OS after the app has been idle for a while.
/// </summary>
/// <remarks>
/// A tray app can sit untouched for hours, but the GC only gives pages back
/// under memory pressure and the working set keeps anything it has touched.
/// When no window is visible this runs a compacting collection and asks Windows
/// to trim the working set - the same thing a minimised Win32 app does. It is
/// deliberately one-shot per idle period: the pages fault back in on the next
/// interaction, and trimming in a loop would just churn.
/// </remarks>
internal sealed class IdleMemoryTrimmer : IDisposable
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    private readonly DispatcherTimer _timer;
    private DateTime _lastActivity = DateTime.UtcNow;
    private bool _trimmed;
    private bool _disposed;

    public IdleMemoryTrimmer()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = PollInterval,
        };

        _timer.Tick += OnTick;
        _timer.Start();
    }

    /// <summary>Marks the app as active, which postpones the next trim.</summary>
    public void Touch()
    {
        _lastActivity = DateTime.UtcNow;
        _trimmed = false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTick;
        GC.SuppressFinalize(this);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_trimmed || DateTime.UtcNow - _lastActivity < IdleDelay)
        {
            return;
        }

        if (Application.Current is { } app
            && app.Windows.OfType<Window>().Any(window => window.IsVisible))
        {
            return;
        }

        _trimmed = true;
        Trim();
    }

    private static void Trim()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();

        // -1 for both sizes means "trim as much as possible".
        _ = SetProcessWorkingSetSize(GetCurrentProcess(), new IntPtr(-1), new IntPtr(-1));
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessWorkingSetSize(IntPtr process, IntPtr minimum, IntPtr maximum);
}
