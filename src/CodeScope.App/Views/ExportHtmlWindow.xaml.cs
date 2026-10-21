using System;
using System.IO;
using System.Windows;

namespace CodeScope.Views;

/// <summary>
/// Asks where the HTML report should be written. The folder is optional: an
/// empty folder means "beside the analysed code", which is what the engine
/// does by default.
/// </summary>
public partial class ExportHtmlWindow : Window
{
    public ExportHtmlWindow()
    {
        InitializeComponent();

        // Paint the caption with the application's own colours; the stock
        // light title bar does not belong on a dark dialog.
        Services.WindowTheme.Attach(this);

        NameBox.Text = "scc-report.html";
        NameBox.Select(0, NameBox.Text.Length);
    }

    /// <summary>The file name the report should be written under.</summary>
    public string ReportFileName => NameBox.Text.Trim();

    /// <summary>The folder to write into; empty means the analysed folder.</summary>
    public string ReportFolder => FolderBox.Text.Trim();

    /// <summary>Pre-fills the file name field.</summary>
    public void SetFileNameForDialog(string name)
    {
        NameBox.Text = string.IsNullOrWhiteSpace(name) ? "scc-report.html" : name;
        NameBox.Select(0, NameBox.Text.Length);
    }

    /// <summary>Opens the shell's own folder picker.</summary>
    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Choose where the HTML report should be written",
            Multiselect = false,
        };

        if (Directory.Exists(FolderBox.Text))
        {
            dialog.InitialDirectory = FolderBox.Text;
        }

        if (dialog.ShowDialog(this) == true)
        {
            FolderBox.Text = dialog.FolderName;
        }
    }

    private void OnExport(object sender, RoutedEventArgs e)
    {
        var name = ReportFileName;
        if (name.Length == 0)
        {
            MessageBox.Show(
                this,
                "Give the report a file name before exporting it.",
                "CodeScope",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            NameBox.Focus();
            return;
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            MessageBox.Show(
                this,
                "The file name contains characters Windows does not allow.",
                "CodeScope",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            NameBox.Focus();
            return;
        }

        if (Path.GetExtension(name).Length == 0)
        {
            name += ".html";
        }

        NameBox.Text = name;
        DialogResult = true;
    }
}
