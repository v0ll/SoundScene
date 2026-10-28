using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using SoundScene.Core.Hotkeys;

namespace SoundScene.App.Services;

/// <summary>
/// Registers system-wide hotkeys through <c>RegisterHotKey</c>.
/// </summary>
/// <remarks>
/// RegisterHotKey needs a window handle, so this owns a message-only window
/// (HWND_MESSAGE) that exists purely to receive WM_HOTKEY. No visible window,
/// no hooking, no keyboard state polling.
/// </remarks>
public sealed class HotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const int ModNoRepeat = 0x4000;
    private static readonly IntPtr HwndMessage = new(-3);

    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _callbacks = [];
    private int _nextId = 0x1000;
    private bool _disposed;

    public HotkeyService()
    {
        var parameters = new HwndSourceParameters("SoundScene.HotkeyWindow")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ParentWindow = HwndMessage,
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    /// <summary>
    /// Registers a gesture. Returns false with a message when another
    /// application already owns the combination.
    /// </summary>
    public bool TryRegister(HotkeyGesture gesture, Action callback, out string? error)
    {
        ArgumentNullException.ThrowIfNull(gesture);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var id = _nextId++;
        var modifiers = ToNative(gesture.Modifiers) | ModNoRepeat;

        if (!RegisterHotKey(_source.Handle, id, modifiers, gesture.VirtualKey))
        {
            error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            return false;
        }

        _callbacks[id] = callback;
        error = null;
        return true;
    }

    /// <summary>Releases every registration so settings can be re-applied.</summary>
    public void UnregisterAll()
    {
        foreach (var id in _callbacks.Keys)
        {
            UnregisterHotKey(_source.Handle, id);
        }

        _callbacks.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmHotkey)
        {
            return IntPtr.Zero;
        }

        if (_callbacks.TryGetValue(wParam.ToInt32(), out var callback))
        {
            callback();
            handled = true;
        }

        return IntPtr.Zero;
    }

    private static uint ToNative(HotkeyModifiers modifiers)
    {
        uint value = 0;

        if (modifiers.HasFlag(HotkeyModifiers.Alt))
        {
            value |= 0x0001;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Control))
        {
            value |= 0x0002;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Shift))
        {
            value |= 0x0004;
        }

        if (modifiers.HasFlag(HotkeyModifiers.Win))
        {
            value |= 0x0008;
        }

        return value;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UnregisterAll();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
