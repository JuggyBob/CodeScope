using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CodeScope.Services;
using CodeScope.ViewModels;

namespace CodeScope;

/// <summary>The application's single main window.</summary>
public partial class MainWindow : Window
{
    /// <summary>How wide the history sidebar is when it is open.</summary>
    private const double SidebarWidth = 300;

    private bool _sidebarExpanded = true;

    public MainWindow()
    {
        InitializeComponent();
        Services.WindowTheme.Attach(this);
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var store = new SettingsStore();
        var theme = new ThemeManager();
        var backend = new BackendService();
        var runner = new ProcessRunner();
        var analysis = new AnalysisService(runner, backend);

        var viewModel = new MainViewModel(store, backend, analysis, theme);
        DataContext = viewModel;

        await viewModel.InitializeAsync();
        StartElapsedTimer(viewModel);

        // The sidebar starts where the user left it, without a jump.
        _sidebarExpanded = viewModel.IsSidebarExpanded;
        SidebarPanel.Width = _sidebarExpanded ? SidebarWidth : 0;
        SidebarContent.Opacity = _sidebarExpanded ? 1 : 0;

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            viewModel.Dispose();
        }

        StopElapsedTimer();
    }

    // ------------------------------------------------------------------
    // Motion
    // ------------------------------------------------------------------

    /// <summary>Clicking a run in the history sidebar reopens its report.</summary>
    private void OnHistorySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        if (e.AddedItems.Count == 0)
        {
            return;
        }

        if (e.AddedItems[0] is HistoryEntryViewModel entry)
        {
            viewModel.OpenHistoryEntryCommand.Execute(entry);
        }

        // Clear the selection so the same run can be opened again later.
        if (sender is System.Windows.Controls.ListBox list)
        {
            list.SelectedItem = null;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainViewModel viewModel)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(MainViewModel.IsSidebarExpanded):
                AnimateSidebar(viewModel.IsSidebarExpanded);
                break;

            case nameof(MainViewModel.ActiveView):
                AnimateViewEntrance();
                break;

            case nameof(MainViewModel.Result):
                viewModel.RefreshElapsed();
                break;
        }
    }

    /// <summary>
    /// Opens or closes the history sidebar. The content fades first when the
    /// sidebar closes, so the labels are never seen being squeezed.
    /// </summary>
    private void AnimateSidebar(bool expand)
    {
        if (expand == _sidebarExpanded)
        {
            return;
        }

        _sidebarExpanded = expand;

        if (expand)
        {
            var width = new DoubleAnimation(SidebarWidth, TimeSpan.FromMilliseconds(280))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            SidebarPanel.BeginAnimation(FrameworkElement.WidthProperty, width);

            var fade = new DoubleAnimation(1, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            };
            SidebarContent.BeginAnimation(UIElement.OpacityProperty, fade);
            return;
        }

        var fadeOut = new DoubleAnimation(0, TimeSpan.FromMilliseconds(140));
        fadeOut.Completed += (_, _) =>
        {
            // The user may have reopened the sidebar while it was fading.
            if (_sidebarExpanded)
            {
                return;
            }

            var width = new DoubleAnimation(0, TimeSpan.FromMilliseconds(240))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
            };
            SidebarPanel.BeginAnimation(FrameworkElement.WidthProperty, width);
        };
        SidebarContent.BeginAnimation(UIElement.OpacityProperty, fadeOut);
    }

    /// <summary>Lifts the incoming view into place so switching feels physical.</summary>
    private void AnimateViewEntrance()
    {
        var translate = new TranslateTransform();
        ViewHost.RenderTransform = translate;

        var slide = new DoubleAnimation(16, 0, TimeSpan.FromMilliseconds(280))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };

        translate.BeginAnimation(TranslateTransform.YProperty, slide);
        ViewHost.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    private static DispatcherTimer? _elapsedTimer;

    private static void StartElapsedTimer(MainViewModel viewModel)
    {
        _elapsedTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _elapsedTimer.Tick += (_, _) => viewModel.RefreshElapsed();
        _elapsedTimer.Start();
    }

    private static void StopElapsedTimer()
    {
        if (_elapsedTimer == null)
        {
            return;
        }

        _elapsedTimer.Stop();
        _elapsedTimer = null;
    }
}
