using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using SoundScene.App.Services;
using SoundScene.Core.Backends.Dolby;

namespace SoundScene.App.Views;

/// <summary>
/// Explains what SoundScene needs and lets the user fix it without guessing.
/// </summary>
/// <remarks>
/// The ROG audio driver installs the DAX service and APOs, but the client API
/// is part of the Dolby Access package. The Store is the only supported way to
/// get it; the app never bundles Dolby binaries.
/// </remarks>
public partial class RequirementsWindow : Window
{
    private const string DolbyStoreUri = "ms-windows-store://pdp/?productid=9N0866FS04W8";

    private static readonly Brush OkBrush = new SolidColorBrush(Color.FromRgb(0x6B, 0xCB, 0x77));
    private static readonly Brush MissingBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x8A));

    private DaxRequirements _requirements;

    public RequirementsWindow(DaxRequirements requirements)
    {
        ArgumentNullException.ThrowIfNull(requirements);

        _requirements = requirements;

        InitializeComponent();
        DarkTitleBar.Apply(this);
        Render();
    }

    /// <summary>True when the checks passed by the time the window closed.</summary>
    public bool IsReady => _requirements.IsReady;

    private void Render()
    {
        RequirementList.ItemsSource = _requirements.Items
            .Select(item => new RequirementRow(
                item.Name,
                item.Detail,
                item.Ok ? "\u2713" : "\u2715",
                item.Ok ? OkBrush : MissingBrush))
            .ToArray();

        StatusText.Text = _requirements.IsReady
            ? "Everything is ready. SoundScene will use Dolby Access as soon as you continue."
            : "Install the missing item(s), then click Recheck. You can continue now and finish setup later from the tray menu.";

        ContinueButton.Content = _requirements.IsReady ? "Continue" : "Continue anyway";
    }

    private void OnStore(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(DolbyStoreUri) { UseShellExecute = true });
            StatusText.Text = "The Microsoft Store is opening. Install Dolby Access, then click Recheck.";
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            StatusText.Text = "Could not open the Store. Install \"Dolby Access\" from the Microsoft Store manually.";
        }
    }

    private void OnRecheck(object sender, RoutedEventArgs e)
    {
        _requirements = DaxRequirementChecker.Check();
        Render();
    }

    private void OnContinue(object sender, RoutedEventArgs e) => Close();

    private sealed record RequirementRow(string Name, string Detail, string Glyph, Brush Brush)
    {
        public override string ToString() => Name;
    }
}
