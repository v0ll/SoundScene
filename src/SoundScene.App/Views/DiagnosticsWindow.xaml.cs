using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using SoundScene.App.Services;
using SoundScene.Core.Audio;

namespace SoundScene.App.Views;

/// <summary>
/// Shows the backend's raw state so a user can attach it to a bug report.
/// </summary>
/// <remarks>
/// This is undocumented surface, so when something breaks the answer is
/// usually in here: HRESULTs, endpoint names, the copied DLL path.
/// </remarks>
public partial class DiagnosticsWindow : Window
{
    private readonly IAudioBackend _backend;

    public DiagnosticsWindow(IAudioBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);

        InitializeComponent();
        DarkTitleBar.Apply(this);

        _backend = backend;
        Loaded += async (_, _) => await LoadAsync().ConfigureAwait(true);
    }

    private async Task LoadAsync()
    {
        var captured = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
        string body;

        try
        {
            body = await _backend.GetDiagnosticsAsync().ConfigureAwait(true);
            SummaryText.Text = $"{_backend.Name} \u00B7 captured {captured}";
        }
        catch (Exception ex)
        {
            body = string.Join(
                Environment.NewLine,
                "The backend could not be opened.",
                string.Empty,
                ex.ToString());

            SummaryText.Text = "The backend is unavailable \u00B7 details below";
        }

        DumpBox.Text = string.Join(
            Environment.NewLine,
            $"SoundScene {Version()}",
            $"backend  : {_backend.Name}",
            $"captured : {captured}",
            string.Empty,
            body);

        // A read-only TextBox scrolls to the caret, which is the end of the
        // text. Start at the top, where the useful lines are.
        DumpBox.CaretIndex = 0;
        DumpBox.ScrollToHome();
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Windows.Clipboard.SetText(DumpBox.Text);
            SummaryText.Text = "Copied to the clipboard";
        }
        catch (ExternalException)
        {
            SummaryText.Text = "The clipboard is busy \u2014 try again";
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private static string Version() =>
        typeof(DiagnosticsWindow).Assembly.GetName().Version?.ToString() ?? "dev";
}
