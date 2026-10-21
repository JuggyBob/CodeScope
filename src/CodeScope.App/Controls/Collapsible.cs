using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace CodeScope.Controls;

/// <summary>
/// A card whose header opens and closes its content. The difference from the
/// stock <see cref="Expander"/> is the motion: the content grows and fades
/// instead of appearing, and the header carries a single chevron that turns as
/// the card opens.
/// </summary>
public sealed class Collapsible : ContentControl
{
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(object), typeof(Collapsible),
        new PropertyMetadata(null));

    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
        nameof(IsExpanded), typeof(bool), typeof(Collapsible),
        new PropertyMetadata(false, OnIsExpandedChanged));

    static Collapsible()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(Collapsible), new FrameworkPropertyMetadata(typeof(Collapsible)));
    }

    public Collapsible()
    {
        IsExpanded = false;
    }

    /// <summary>The content of the header row — a title, a badge, anything.</summary>
    public object Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>True while the content is open.</summary>
    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((Collapsible)d).Animate((bool)e.NewValue);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (GetTemplateChild("PART_ContentHost") is not FrameworkElement host)
        {
            return;
        }

        // A template that is applied again — a theme change, for instance —
        // starts from the state the card is in, not from a stale animation.
        host.BeginAnimation(HeightProperty, null);
        host.BeginAnimation(OpacityProperty, null);
        host.Visibility = IsExpanded ? Visibility.Visible : Visibility.Collapsed;
        host.Height = IsExpanded ? double.NaN : 0d;
        host.Opacity = IsExpanded ? 1d : 0d;
    }

    /// <summary>
    /// How tall the content is when it is laid out at the width the card
    /// actually has. Measuring it without a width would leave text unwrapped
    /// and the card the wrong height.
    /// </summary>
    private double MeasureContent()
    {
        if (GetTemplateChild("PART_Content") is not FrameworkElement content)
        {
            return 0d;
        }

        var width = ActualWidth;
        if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
        {
            width = double.PositiveInfinity;
        }

        content.Measure(new Size(width, double.PositiveInfinity));

        var height = content.DesiredSize.Height;
        return double.IsNaN(height) || double.IsInfinity(height) ? 0d : height;
    }

    /// <summary>Grows the content open or folds it away.</summary>
    private void Animate(bool expanded)
    {
        if (GetTemplateChild("PART_ContentHost") is not FrameworkElement host)
        {
            return;
        }

        if (expanded)
        {
            host.Visibility = Visibility.Visible;

            // Measure the content itself: the host is pinned to a height of
            // zero while it is closed, so measuring the host would report
            // nothing to grow into.
            var target = MeasureContent();

            var grow = new DoubleAnimation(0, target, TimeSpan.FromMilliseconds(240))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            grow.Completed += (_, _) =>
            {
                // Hand the height back to the layout, so the card keeps fitting
                // its content when the window is resized.
                host.BeginAnimation(HeightProperty, null);
                host.Height = double.NaN;
            };

            var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };

            host.BeginAnimation(HeightProperty, grow);
            host.BeginAnimation(OpacityProperty, fade);
            return;
        }

        // The host may be auto-sized, in which case its height is not a number
        // and cannot be animated. Measure the content to learn how tall the
        // card currently is, then fold it away from there.
        var current = host.Height;
        if (double.IsNaN(current) || double.IsInfinity(current))
        {
            current = MeasureContent();
        }

        var fold = new DoubleAnimation(current, 0, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
        };
        fold.Completed += (_, _) =>
        {
            host.Visibility = Visibility.Collapsed;
            host.BeginAnimation(HeightProperty, null);
            host.BeginAnimation(OpacityProperty, null);
            host.Height = 0d;
            host.Opacity = 0d;
        };

        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(140));

        host.BeginAnimation(HeightProperty, fold);
        host.BeginAnimation(OpacityProperty, fadeOut);
    }
}
