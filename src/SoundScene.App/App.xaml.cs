using System.Diagnostics.CodeAnalysis;
using System.Windows;
using SoundScene.App.Services;
using SoundScene.App.ViewModels;
using SoundScene.App.Views;
using SoundScene.Core.Backends.Dolby;
using Application = System.Windows.Application;

namespace SoundScene.App;

[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable",
    Justification = "The WPF Application lifetime is the owner; both fields are disposed in OnExit.")]
public partial class App : Application
{
    private DolbyDaxBackend? _backend;
    private TrayViewModel? _viewModel;
    private PopupWindow? _popup;
    private TrayIconService? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _backend = new DolbyDaxBackend();
        _viewModel = new TrayViewModel(_backend);
        _popup = new PopupWindow { DataContext = _viewModel };

        _tray = new TrayIconService();
        _tray.PopupRequested += OnPopupRequested;
        _tray.ExitRequested += OnExitRequested;

        _ = _viewModel.RefreshAsync();
    }

    private async void OnPopupRequested(object? sender, EventArgs e)
    {
        if (_popup is null || _viewModel is null)
        {
            return;
        }

        _popup.ShowNearTray();
        await _viewModel.RefreshAsync().ConfigureAwait(true);
    }

    private void OnExitRequested(object? sender, EventArgs e) => Shutdown();

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();

        if (_backend is not null)
        {
            _backend.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        base.OnExit(e);
    }
}
