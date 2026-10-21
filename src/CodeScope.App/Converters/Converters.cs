using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace CodeScope.Converters;

/// <summary>Common value converters. All are stateless and shareable.</summary>

/// <summary>
/// Reports whether a table column is the one the user sorted by, and which
/// way the rows run.
///
/// The values are, in order: the report's sort version, the report, the table
/// the column belongs to ("Files", "Hotspots") and the column's heading. The
/// heading is what the report remembers, so a heading that appears in two
/// tables still points at the right column.
/// </summary>
public sealed class SortIndicatorConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var heading = values.Length > 3 ? values[3] as string : null;
        var table = values.Length > 2 ? values[2] as string : null;
        var report = values.Length > 1 ? values[1] as CodeScope.ViewModels.ReportViewModel : null;

        var state = report?.SortState(table ?? string.Empty);
        var descending = state?.Descending ?? false;

        if (parameter as string == "rotation")
        {
            // The chevron is drawn pointing down, so ascending turns it around.
            return descending ? 0d : 180d;
        }

        if (state == null || string.IsNullOrEmpty(heading) || string.IsNullOrEmpty(state.Heading))
        {
            return Visibility.Collapsed;
        }

        return string.Equals(state.Heading, heading, StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var flag = value is true;
        return flag ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

[ValueConversion(typeof(bool), typeof(Visibility))]
public sealed class InvertedBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var flag = value is true;
        return flag ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

[ValueConversion(typeof(string), typeof(Visibility))]
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

[ValueConversion(typeof(int), typeof(Visibility))]
public sealed class IntGreaterThanZeroConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is int i && i > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

[ValueConversion(typeof(bool), typeof(FontWeight))]
public sealed class BoolToFontWeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? FontWeights.SemiBold : FontWeights.Normal;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Converts between a comma-separated text box and the option's string
/// collection, which is how the engine's repeatable flags are presented.
/// </summary>
public sealed class StringListConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not System.Collections.Generic.IEnumerable<string> list)
        {
            return string.Empty;
        }

        return string.Join(", ", list);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = value as string ?? string.Empty;
        var result = new System.Collections.Generic.List<string>();
        foreach (var part in text.Split(',', System.StringSplitOptions.RemoveEmptyEntries |
                                                System.StringSplitOptions.TrimEntries))
        {
            if (part.Length > 0 && !result.Contains(part))
            {
                result.Add(part);
            }
        }

        return result;
    }
}

/// <summary>Maps a series index (0-based) onto the shared categorical palette.</summary>
public sealed class SeriesIndexToBrushConverter : IValueConverter
{
    /// <summary>The theme brushes the palette is made of, in order.</summary>
    private static readonly string[] PaletteKeys =
    {
        "Series1Brush",
        "Series2Brush",
        "Series3Brush",
        "Series4Brush",
        "Series5Brush",
        "Series6Brush",
        "Series7Brush",
        "Series8Brush",
    };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var index = value is int i ? i : 0;
        index = ((index % PaletteKeys.Length) + PaletteKeys.Length) % PaletteKeys.Length;

        // The palette belongs to the theme, so a chart follows the light and
        // the dark theme instead of keeping the colours of one of them.
        if (Application.Current?.TryFindResource(PaletteKeys[index]) is Brush brush)
        {
            return brush;
        }

        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
