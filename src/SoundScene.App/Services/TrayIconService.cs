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
        var settings = new ToolStripMenuItem("Hotkey settings\u2026", null,
            (_, _) => SettingsRequested?.Invoke(this, EventArgs.Empty));
        var equalizer = new ToolStripMenuItem("Graphic equalizer\u2026", null,
            (_, _) => EqualizerRequested?.Invoke(this, EventArgs.Empty));

        // CheckOnClick flips Checked before Click fires, so the handler can just
        // write the new state through.
        var startup = new ToolStripMenuItem("Start with Windows")
        {
            CheckOnClick = true,
            Checked = StartupRegistration.IsEnabled,
        };
        startup.Click += (_, _) => StartupRegistration.SetEnabled(startup.Checked);

        var exit = new ToolStripMenuItem("Exit", null, (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty));

        menu.Items.Add(open);
        menu.Items.Add(equalizer);
        menu.Items.Add(settings);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(startup);
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

    public event EventHandler? SettingsRequested;

    public event EventHandler? EqualizerRequested;

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
