using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CodeScope.Services;
using CodeScope.ViewModels;

namespace CodeScope.Views;

/// <summary>
/// The in-app report dashboard. Every section is rendered from the engine's own
/// report data with native controls — nothing is written to disk in this mode.
/// </summary>
public partial class ReportView : UserControl
{
    public ReportView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    /// <summary>The report slides into place when it first appears.</summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var translate = new System.Windows.Media.TranslateTransform();
        Body.RenderTransform = translate;

        var slide = new System.Windows.Media.Animation.DoubleAnimation(18, 0, TimeSpan.FromMilliseconds(320))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase
            {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut,
            },
        };

        var fade = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(280))
        {
            EasingFunction = new System.Windows.Media.Animation.CubicEase
            {
                EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut,
            },
        };

        translate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slide);
        Body.BeginAnimation(System.Windows.UIElement.OpacityProperty, fade);
    }

    /// <summary>
    /// A click on a column heading sorts the table by that column. The report
    /// orders every table itself, so the arrow in the heading and the order of
    /// the rows always agree.
    /// </summary>
    private void OnTableSorting(object sender, DataGridSortingEventArgs e)
    {
        if (sender is not DataGrid grid || e.Column is not { } column)
        {
            return;
        }

        var heading = column.Header as string ?? string.Empty;
        var key = column.SortMemberPath;
        if (heading.Length == 0 || string.IsNullOrEmpty(key))
        {
            return;
        }

        var table = grid.Tag as string ?? "Files";
        var report = DataContext as ReportViewModel;
        if (report == null)
        {
            return;
        }

        // The report orders the rows, so the grid must not sort them a second
        // time on its own.
        e.Handled = true;

        // The report decides the direction: the same heading turns the table
        // around, a new heading starts with the smallest value on top.
        report.ToggleSort(table, heading, key);
    }

    /// <summary>Double-clicking a per-file row reveals the file in File Explorer.</summary>
    private void OnFilesGridDoubleClick(object sender, MouseButtonEventArgs e)
        => RevealGridRow<ReportFileRow>(sender, e, row => row.Location);

    /// <summary>
    /// Double-clicking a row under "Longest lines" reveals the file in File
    /// Explorer. This is the quickest way from an awkwardly long line to the
    /// code itself.
    /// </summary>
    private void OnOutliersGridDoubleClick(object sender, MouseButtonEventArgs e)
        => RevealGridRow<OutlierRowView>(sender, e, row => row.File);

    /// <summary>Double-clicking a hotspot row reveals the file in File Explorer.</summary>
    private void OnHotspotsGridDoubleClick(object sender, MouseButtonEventArgs e)
        => RevealGridRow<HotspotRowView>(sender, e, row => row.File);

    /// <summary>
    /// Double-clicking a coupling row reveals the file in the column under the
    /// pointer: File B when the right-hand file column was hit, File A otherwise.
    /// </summary>
    private void OnCouplingGridDoubleClick(object sender, MouseButtonEventArgs e)
        => RevealGridRow<CouplingRowView>(sender, e, row => ClickedColumnIndex(sender, e) == 1 ? row.FileB : row.FileA);

    /// <summary>
    /// Resolves a report-relative location against the analysed root and asks
    /// Explorer to show it. A path the engine could not match falls back to
    /// opening the project folder rather than failing silently.
    /// </summary>
    private void RevealGridRow<TRow>(object sender, MouseButtonEventArgs e, Func<TRow, string?> location)
        where TRow : class
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (sender is not DataGrid grid || grid.SelectedItem is not TRow row)
        {
            return;
        }

        if (DataContext is not ReportViewModel viewModel)
        {
            return;
        }

        var value = location(row);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var root = viewModel.Data.Path;
        var full = Path.IsPathRooted(value)
            ? value
            : Path.Combine(root ?? string.Empty, value);

        if (File.Exists(full))
        {
            BackendService.OpenInFileExplorer(full);
        }
        else if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
        {
            BackendService.OpenInFileExplorer(root);
        }
    }

    /// <summary>Index of the column under the pointer, or -1 outside any cell.</summary>
    private static int ClickedColumnIndex(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid)
        {
            return -1;
        }

        var cell = FindAncestor<DataGridCell>(e.OriginalSource as DependencyObject);
        return cell == null ? -1 : grid.Columns.IndexOf(cell.Column);
    }

    private static T? FindAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match)
            {
                return match;
            }

            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
    }
}
