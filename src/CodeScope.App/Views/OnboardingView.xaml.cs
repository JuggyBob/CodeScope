using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace CodeScope.Views;

/// <summary>
/// The first-run walkthrough. It is an overlay on top of the normal shell,
/// so nothing about the application is hidden behind it and every step can
/// be left at any time.
/// </summary>
public partial class OnboardingView : UserControl
{
    public OnboardingView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => AnimateIn();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyPropertyChanged old)
        {
            old.PropertyChanged -= OnViewModelPropertyChanged;
        }

        if (e.NewValue is INotifyPropertyChanged current)
        {
            current.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModels.MainViewModel.OnboardingStep))
        {
            AnimateStep();
        }
    }

    /// <summary>Fades and lifts the card into place when the walkthrough opens.</summary>
    private void AnimateIn()
    {
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        var scale = new ScaleTransform(0.97, 0.97);
        Card.RenderTransform = scale;
        Card.RenderTransformOrigin = new Point(0.5, 0.5);

        var grow = new DoubleAnimation(1, TimeSpan.FromMilliseconds(280))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        Card.BeginAnimation(OpacityProperty, fade);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
    }

    /// <summary>Slides the body slightly as the user moves between steps.</summary>
    private void AnimateStep()
    {
        var body = this.FindName("Card") as FrameworkElement;
        if (body == null)
        {
            return;
        }

        var translate = new TranslateTransform();
        body.RenderTransform = translate;

        var slide = new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        var fade = new DoubleAnimation(0.35, 1, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        translate.BeginAnimation(TranslateTransform.YProperty, slide);
        body.BeginAnimation(OpacityProperty, fade);
    }
}
