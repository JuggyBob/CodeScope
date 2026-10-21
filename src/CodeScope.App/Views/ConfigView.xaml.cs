using System.Windows.Controls;
using CodeScope.ViewModels;

namespace CodeScope.Views;

/// <summary>
/// The configuration screen: project folder, report contents, output mode,
/// presets and the advanced settings card.
/// </summary>
public partial class ConfigView : UserControl
{
    public ConfigView()
    {
        InitializeComponent();
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    /// <summary>
    /// Apply the preset the user just picked. Re-selecting the preset that is
    /// already loaded is a no-op inside the view model, so the list's initial
    /// selection echo cannot wipe a remembered session.
    /// </summary>
    private void OnPresetSelected(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel == null || e.AddedItems.Count == 0)
        {
            return;
        }

        if (e.AddedItems[0] is string name)
        {
            ViewModel.ApplyPreset(name);
        }
    }
}
