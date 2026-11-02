using System.Windows;
using System.Windows.Input;
using SoundScene.App.Services;
using SoundScene.App.ViewModels;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;

namespace SoundScene.App.Views;

/// <summary>Graphic EQ editor for the Personalize profile.</summary>
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

    // The dB boxes commit on focus loss; Enter commits and keeps the caret in
    // the box so a value can be typed over without touching the mouse.
    private void OnValueKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox box)
        {
            return;
        }

        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        box.SelectAll();
        e.Handled = true;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
