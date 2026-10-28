using System.Drawing;
using System.Windows.Forms;

namespace SoundScene.App.Services;

/// <summary>
/// Owns the notification-area icon and its context menu.
/// WinForms NotifyIcon is used deliberately: it is in the box, stable, and the
/// tray is the only place this app needs WinForms.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private const int MaxTooltipLength = 63;

    private readonly Icon _icon;
    private readonly NotifyIcon _notifyIcon;
    private bool _disposed;

    public TrayIconService()
    {
        _icon = AppIcon.Create();

        var menu = new ContextMenuStrip();
        var open = new ToolStripMenuItem("Open SoundScene", null, (_, _) => PopupRequested?.Invoke(this, EventArgs.Empty))
        {
            Font = new Font(SystemFonts.MenuFont!, FontStyle.Bold),
        };
        var exit = new ToolStripMenuItem("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        menu.Items.Add(open);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exit);

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon,
            Text = "SoundScene",
            Visible = true,
            ContextMenuStrip = menu,
        };

        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                PopupRequested?.Invoke(this, EventArgs.Empty);
            }
        };
    }

    public event EventHandler? PopupRequested;

    public event EventHandler? ExitRequested;

    public void SetStatus(string text)
    {
        if (_disposed)
        {
            return;
        }

        _notifyIcon.Text = text.Length <= MaxTooltipLength ? text : text[..MaxTooltipLength];
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
        _icon.Dispose();
    }
}
