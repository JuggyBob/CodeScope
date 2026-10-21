using System.Windows;

namespace CodeScope.Views;

/// <summary>Small prompt used to name a saved preset.</summary>
public partial class PresetNameWindow : Window
{
    public PresetNameWindow()
    {
        InitializeComponent();
        Services.WindowTheme.Attach(this);
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    /// <summary>The name typed by the user.</summary>
    public string PresetName
    {
        get => NameBox.Text;
        set => NameBox.Text = value;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            return;
        }

        DialogResult = true;
    }
}