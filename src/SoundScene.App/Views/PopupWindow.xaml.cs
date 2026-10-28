using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace SoundScene.App.Views;

/// <summary>
/// Compact switcher shown from the tray. Hides itself when it loses focus,
/// which is what every tray popup does and what users expect.
/// </summary>
public partial class PopupWindow : Window
{
    public PopupWindow()
    {
        InitializeComponent();
    }

    /// <summary>Raised when the user asks for the equalizer from the header button.</summary>
    public event EventHandler? EqualizerRequested;

    /// <summary>Shows the popup anchored to the bottom-right of the work area.</summary>
    public void ShowNearTray()
    {
        Show();
        UpdateLayout();

        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - ActualWidth - 4;
        Top = workArea.Bottom - ActualHeight - 4;

        Activate();
        Focus();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        // A modal child (the scene name prompt) takes focus without the user
        // meaning to dismiss the popup, so only hide when nothing is owned.
        if (OwnedWindows.Count > 0)
        {
            return;
        }

        Hide();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Hide();
        }
    }

    private void OnEqualizerClick(object sender, RoutedEventArgs e) =>
        EqualizerRequested?.Invoke(this, EventArgs.Empty);
}
