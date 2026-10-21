using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CodeScope.Controls;

/// <summary>
/// Draws an icon on the grid every icon in the app is drawn on.
///
/// The geometries are authored on a 24-unit square (see Resources/Styles/
/// Icons.xaml). This control scales that square as a whole into whatever
/// room it is given, so every glyph shrinks by the same factor and keeps the
/// same weight — and a glyph is never cropped, however small its box is.
/// </summary>
public sealed class IconView : Viewbox
{
    /// <summary>The side of the grid the icon geometries are drawn on.</summary>
    public const double Grid = 24d;

    private readonly Path _path = new();
    private readonly Canvas _canvas = new() { Width = Grid, Height = Grid };

    public static readonly DependencyProperty GeometryProperty = DependencyProperty.Register(
        nameof(Geometry),
        typeof(Geometry),
        typeof(IconView),
        new FrameworkPropertyMetadata(null, OnGeometryChanged));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke),
        typeof(Brush),
        typeof(IconView),
        new FrameworkPropertyMetadata(Brushes.Transparent, OnStrokeChanged));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness),
        typeof(double),
        typeof(IconView),
        new FrameworkPropertyMetadata(1.5, OnStrokeThicknessChanged));

    public static readonly DependencyProperty FillProperty = DependencyProperty.Register(
        nameof(Fill),
        typeof(Brush),
        typeof(IconView),
        new FrameworkPropertyMetadata(null, OnFillChanged));

    /// <summary>The glyph to draw.</summary>
    public Geometry? Geometry
    {
        get => (Geometry?)GetValue(GeometryProperty);
        set => SetValue(GeometryProperty, value);
    }

    /// <summary>The colour of the line.</summary>
    public Brush? Stroke
    {
        get => (Brush?)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    /// <summary>How thick the line is, in grid units.</summary>
    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    /// <summary>The colour of the filled shape, for glyphs that are filled.</summary>
    public Brush? Fill
    {
        get => (Brush?)GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    public IconView()
    {
        Stretch = Stretch.Uniform;

        // The glyph is drawn on the grid as it is; the viewbox does the
        // scaling, so the stroke thins with it.
        _path.Stretch = Stretch.None;
        _path.StrokeLineJoin = PenLineJoin.Round;
        _path.StrokeStartLineCap = PenLineCap.Round;
        _path.StrokeEndLineCap = PenLineCap.Round;

        _canvas.Children.Add(_path);
        Child = _canvas;
    }

    private static void OnGeometryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((IconView)d)._path.Data = e.NewValue as Geometry;

    private static void OnStrokeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((IconView)d)._path.Stroke = e.NewValue as Brush;

    private static void OnStrokeThicknessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((IconView)d)._path.StrokeThickness = (double)e.NewValue;

    private static void OnFillChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((IconView)d)._path.Fill = e.NewValue as Brush;
}
