using System.Windows;
using SoundScene.App.Services;
using SoundScene.App.ViewModels;

namespace SoundScene.App.Views;

/// <summary>20-band graphic EQ editor for the Personalize profile.</summary>
public partial class EqWindow : Window
{
    private readonly EqViewModel _viewModel;

    public EqWindow(EqViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;

        DarkTitleBar.Apply(this);

        Loaded += async (_, _) => await _viewModel.LoadAsync().ConfigureAwait(true);
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
