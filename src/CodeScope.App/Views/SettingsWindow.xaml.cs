using System.Windows;
using CodeScope.ViewModels;

namespace CodeScope.Views;

/// <summary>
/// The Settings window. The theme choice applies immediately; everything else is
/// persisted per Windows user.
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Services.WindowTheme.Attach(this);
        Closed += (_, _) =>
        {
            if (DataContext is SettingsViewModel model)
            {
                model.Close();
            }
        };
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel model)
        {
            model.Close();
        }

        DialogResult = true;
    }
}