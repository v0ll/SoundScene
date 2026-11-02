using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace SoundScene.App.Services;

/// <summary>
/// Owns the notification-area icon and its context menu.
/// </summary>
/// <remarks>
/// This talks to <c>Shell_NotifyIcon</c> directly instead of using WinForms'
/// <c>NotifyIcon</c>. A tray icon is the only WinForms control this app needs,
/// and pulling in WinForms also loads the Windows SDK projections and GDI+ -
/// tens of megabytes of address space for four menu items. The P/Invoke surface
/// here is small and does exactly what the app needs.
/// </remarks>
public sealed class TrayIconService : IDisposable
{
    private const int WmTrayIcon = 0x8001;
    private const int WmNull = 0x0000;
    private const int WmLButtonUp = 0x0202;
    private const int WmRButtonUp = 0x0205;
    private const int WmContextMenu = 0x007B;

    private const uint NifMessage = 0x00000001;
    private const uint NifIcon = 0x00000002;
    private const uint NifTip = 0x00000004;
    private const uint NifShowTip = 0x00000080;
    private const uint NimAdd = 0x00000000;
    private const uint NimModify = 0x00000001;
    private const uint NimDelete = 0x00000002;

    private const uint MfString = 0x00000000;
    private const uint MfChecked = 0x00000008;
    private const uint MfSeparator = 0x00000800;
    private const uint TpmRightButton = 0x0002;
    private const uint TpmReturnCmd = 0x0100;

    private const int CommandOpen = 1;
    private const int CommandEqualizer = 2;
    private const int CommandHotkeys = 3;
    private const int CommandDiagnostics = 4;
    private const int CommandStartup = 5;
    private const int CommandExit = 6;
    private const int CommandSetup = 7;

    private const int MaxTooltipLength = 127;
    private const int IdiApplication = 32512;

    private readonly HwndSource _source;
    private readonly int _taskbarCreatedMessage;
    private readonly bool _allowStartup;
    private readonly IntPtr _icon;
    private readonly IntPtr _largeIcon;
    private readonly bool _ownsIcon;
    private string _tooltip = "SoundScene";
    private bool _added;
    private bool _disposed;

    public TrayIconService(bool allowStartup = true)
    {
        _allowStartup = allowStartup;

        // A top-level (but never shown) window is required: taskbar-created
        // broadcasts only reach top-level windows, and Shell_NotifyIcon needs a
        // handle for its callback message.
        var parameters = new HwndSourceParameters("SoundScene.TrayWindow")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
        _taskbarCreatedMessage = RegisterWindowMessageW("TaskbarCreated");

        (_icon, _largeIcon, _ownsIcon) = LoadIcon();
        AddIcon();
    }

    public event EventHandler? PopupRequested;

    public event EventHandler? SettingsRequested;

    public event EventHandler? EqualizerRequested;

    public event EventHandler? DiagnosticsRequested;

    public event EventHandler? SetupRequested;

    public event EventHandler? ExitRequested;

    public void SetStatus(string text)
    {
        if (_disposed)
        {
            return;
        }

        _tooltip = text.Length <= MaxTooltipLength ? text : text[..MaxTooltipLength];
        ModifyIcon();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_added)
        {
            var data = CreateData();
            data.uFlags = 0;
            _ = Shell_NotifyIconW(NimDelete, ref data);
        }

        _source.RemoveHook(WndProc);
        _source.Dispose();

        if (_ownsIcon)
        {
            if (_icon != IntPtr.Zero)
            {
                _ = DestroyIcon(_icon);
            }

            if (_largeIcon != IntPtr.Zero && _largeIcon != _icon)
            {
                _ = DestroyIcon(_largeIcon);
            }
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmTrayIcon)
        {
            // The low word carries the mouse message; version 3 semantics also
            // deliver WM_CONTEXTMENU for keyboard activation.
            switch ((int)(lParam.ToInt64() & 0xFFFF))
            {
                case WmLButtonUp:
                    PopupRequested?.Invoke(this, EventArgs.Empty);
                    handled = true;
                    break;

                case WmRButtonUp:
                case WmContextMenu:
                    ShowMenu();
                    handled = true;
                    break;
            }
        }
        else if (msg == _taskbarCreatedMessage)
        {
            // Explorer restarted and forgot every icon.
            _added = false;
            AddIcon();
        }

        return IntPtr.Zero;
    }

    private void AddIcon()
    {
        var data = CreateData();
        data.uFlags = NifMessage | NifIcon | NifTip | NifShowTip;
        data.uCallbackMessage = WmTrayIcon;
        data.hIcon = _icon;
        data.szTip = _tooltip;

        _added = Shell_NotifyIconW(NimAdd, ref data);

        if (!_added)
        {
            // One retry; the shell is occasionally not ready right after login.
            _added = Shell_NotifyIconW(NimAdd, ref data);
        }
    }

    private void ModifyIcon()
    {
        if (!_added)
        {
            return;
        }

        var data = CreateData();
        data.uFlags = NifTip | NifShowTip;
        data.szTip = _tooltip;
        _ = Shell_NotifyIconW(NimModify, ref data);
    }

    private NotifyIconData CreateData() => new()
    {
        cbSize = (uint)Marshal.SizeOf<NotifyIconData>(),
        hWnd = _source.Handle,
        uID = 1,
        szTip = _tooltip,
    };

    private void ShowMenu()
    {
        var menu = CreatePopupMenu();
        if (menu == IntPtr.Zero)
        {
            return;
        }

        try
        {
            _ = AppendMenuW(menu, MfString, CommandOpen, "Open SoundScene");
            _ = SetMenuDefaultItem(menu, CommandOpen, 0);
            _ = AppendMenuW(menu, MfSeparator, 0, null);
            _ = AppendMenuW(menu, MfString, CommandEqualizer, "Graphic equalizer\u2026");
            _ = AppendMenuW(menu, MfString, CommandHotkeys, "Hotkeys\u2026");
            _ = AppendMenuW(menu, MfSeparator, 0, null);
            _ = AppendMenuW(menu, MfString, CommandDiagnostics, "Diagnostics\u2026");
            _ = AppendMenuW(menu, MfString, CommandSetup, "Setup requirements\u2026");

            if (_allowStartup)
            {
                _ = AppendMenuW(menu, MfSeparator, 0, null);

                var startupFlags = MfString | (StartupRegistration.IsEnabled ? MfChecked : 0);
                _ = AppendMenuW(menu, startupFlags, CommandStartup, "Start with Windows");
            }

            _ = AppendMenuW(menu, MfSeparator, 0, null);
            _ = AppendMenuW(menu, MfString, CommandExit, "Exit");

            _ = GetCursorPos(out var point);

            // Without this the menu stays on screen when the user clicks away.
            _ = SetForegroundWindow(_source.Handle);

            var command = TrackPopupMenuEx(
                menu,
                TpmRightButton | TpmReturnCmd,
                point.X,
                point.Y,
                _source.Handle,
                IntPtr.Zero);

            _ = PostMessageW(_source.Handle, WmNull, IntPtr.Zero, IntPtr.Zero);
            Dispatch(command);
        }
        finally
        {
            _ = DestroyMenu(menu);
        }
    }

    private void Dispatch(int command)
    {
        switch (command)
        {
            case CommandOpen:
                PopupRequested?.Invoke(this, EventArgs.Empty);
                break;

            case CommandEqualizer:
                EqualizerRequested?.Invoke(this, EventArgs.Empty);
                break;

            case CommandHotkeys:
                SettingsRequested?.Invoke(this, EventArgs.Empty);
                break;

            case CommandDiagnostics:
                DiagnosticsRequested?.Invoke(this, EventArgs.Empty);
                break;

            case CommandSetup:
                SetupRequested?.Invoke(this, EventArgs.Empty);
                break;

            case CommandStartup:
                StartupRegistration.SetEnabled(!StartupRegistration.IsEnabled);
                break;

            case CommandExit:
                ExitRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private static (IntPtr Icon, IntPtr Large, bool Owns) LoadIcon()
    {
        var path = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(path)
            && ExtractIconExW(path, 0, out var large, out var small, 1) > 0)
        {
            var icon = small != IntPtr.Zero ? small : large;
            if (icon != IntPtr.Zero)
            {
                return (icon, large, true);
            }
        }

        // Shared system icon; never destroyed.
        return (LoadIconW(IntPtr.Zero, (IntPtr)IdiApplication), IntPtr.Zero, false);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NotifyIconData
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uID;
        public uint uFlags;
        public uint uCallbackMessage;
        public IntPtr hIcon;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;

        public uint dwState;
        public uint dwStateMask;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;

        public uint uTimeoutOrVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;

        public uint dwInfoFlags;
        public Guid guidItem;
        public IntPtr hBalloonIcon;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Shell_NotifyIconW(uint message, ref NotifyIconData data);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint ExtractIconExW(string file, int index, out IntPtr large, out IntPtr small, uint count);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadIconW(IntPtr instance, IntPtr name);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegisterWindowMessageW(string name);

    [DllImport("user32.dll")]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AppendMenuW(IntPtr menu, uint flags, nuint id, string? text);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetMenuDefaultItem(IntPtr menu, uint item, uint byPosition);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyMenu(IntPtr menu);

    [DllImport("user32.dll")]
    private static extern int TrackPopupMenuEx(
        IntPtr menu,
        uint flags,
        int x,
        int y,
        IntPtr owner,
        IntPtr parameters);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out Point point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
}
