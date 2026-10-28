using System.Windows;

namespace SoundScene.App.Views;

/// <summary>Small modal that asks for a single line of text.</summary>
public partial class PromptWindow : Window
{
    public PromptWindow(string title, string label, string initialValue)
    {
        InitializeComponent();

        Title = title;
        LabelText.Text = label;
        ValueBox.Text = initialValue;

        Loaded += (_, _) =>
        {
            ValueBox.Focus();
            ValueBox.SelectAll();
        };
    }

    /// <summary>Set when the user accepts.</summary>
    public string? Result { get; private set; }

    private void OnAccept(object sender, RoutedEventArgs e)
    {
        var value = ValueBox.Text.Trim();

        if (value.Length == 0)
        {
            HintText.Text = "Give the scene a name.";
            return;
        }

        Result = value;
        DialogResult = true;
    }
}
