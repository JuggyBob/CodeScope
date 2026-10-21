using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace CodeScope.Controls;

/// <summary>
/// A toggle that reads as a switch rather than a checkbox. It behaves like a
/// <see cref="ToggleButton"/> — two-state, keyboard operable, focusable — but
/// is styled as the sliding switch used throughout the interface.
/// </summary>
public class ToggleSwitch : ToggleButton
{
    /// <summary>Raised after the switch flips, in both directions.</summary>
    public event RoutedEventHandler? Toggled;

    static ToggleSwitch()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ToggleSwitch),
            new FrameworkPropertyMetadata(typeof(ToggleSwitch)));
        FocusableProperty.OverrideMetadata(typeof(ToggleSwitch), new FrameworkPropertyMetadata(true));
    }

    public ToggleSwitch()
    {
        ClickMode = ClickMode.Press;
        Cursor = Cursors.Hand;
        HorizontalAlignment = HorizontalAlignment.Left;
        VerticalAlignment = VerticalAlignment.Center;
    }

    protected override void OnChecked(RoutedEventArgs e)
    {
        base.OnChecked(e);
        Toggled?.Invoke(this, e);
    }

    protected override void OnUnchecked(RoutedEventArgs e)
    {
        base.OnUnchecked(e);
        Toggled?.Invoke(this, e);
    }
}
