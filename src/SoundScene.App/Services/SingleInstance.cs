using System.Threading;

namespace SoundScene.App.Services;

/// <summary>
/// Keeps the app to one process per session.
/// </summary>
/// <remarks>
/// Two instances do not just duplicate a tray icon: both would drive the same
/// DAX profile, and whichever writes last wins. That is exactly how an earlier
/// probe appeared to lose its restore. A second launch therefore wakes the
/// running instance and exits.
/// </remarks>
internal sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\SoundScene.Instance";
    private const string ActivationEventName = @"Local\SoundScene.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activation;
    private readonly RegisteredWaitHandle _registration;
    private bool _disposed;

    private SingleInstance(Mutex mutex, EventWaitHandle activation)
    {
        _mutex = mutex;
        _activation = activation;

        _registration = ThreadPool.RegisterWaitForSingleObject(
            activation,
            (_, _) => ActivationRequested?.Invoke(this, EventArgs.Empty),
            state: null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    /// <summary>Raised on a pool thread when another launch asks for the popup.</summary>
    public event EventHandler? ActivationRequested;

    /// <summary>
    /// Returns the instance when this process is the first one, or <c>null</c>
    /// when SoundScene is already running.
    /// </summary>
    public static SingleInstance? TryAcquire()
    {
        var mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);

        if (!createdNew)
        {
            mutex.Dispose();
            return null;
        }

        return new SingleInstance(
            mutex,
            new EventWaitHandle(initialState: false, EventResetMode.AutoReset, ActivationEventName));
    }

    /// <summary>Asks the running instance to show its popup. Best effort.</summary>
    public static void SignalRunningInstance()
    {
        if (EventWaitHandle.TryOpenExisting(ActivationEventName, out var handle))
        {
            using (handle)
            {
                handle.Set();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _registration.Unregister(null);
        _activation.Dispose();

        // Closing the handle is enough to release the mutex, and it avoids the
        // thread-affinity rules of ReleaseMutex during shutdown.
        _mutex.Dispose();
    }
}
