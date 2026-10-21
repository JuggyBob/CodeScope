using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using CodeScope.Models;
using CodeScope.Services;

namespace CodeScope.ViewModels;

/// <summary>The Settings window: theme, persisted preferences and backend facts.</summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private readonly SettingsStore _store;
    private readonly ThemeManager _themeManager;
    private AppTheme _theme;

    public SettingsViewModel(SettingsStore store, ThemeManager themeManager)
    {
        _store = store;
        _themeManager = themeManager;

        _theme = store.LoadSettings().Theme;
        RememberLastConfiguration = _store.LoadSettings().RememberLastConfiguration;

        ApplyThemeCommand = new DelegateCommand<AppTheme>(ApplyTheme);
        OpenDataFolderCommand = new DelegateCommand(_ => OpenDataFolder());
        OpenSnapshotDocsCommand = new DelegateCommand(_ => OpenSnapshotDocs());
    }

    /// <summary>The theme currently selected in the UI.</summary>
    public AppTheme Theme
    {
        get => _theme;
        private set => SetProperty(ref _theme, value);
    }

    public bool RememberLastConfiguration
    {
        get => _rememberLastConfiguration;
        set => SetProperty(ref _rememberLastConfiguration, value);
    }

    private bool _rememberLastConfiguration = true;

    public bool IsDarkSelected => Theme == AppTheme.Dark;

    public bool IsLightSelected => Theme == AppTheme.Light;

    public bool IsSystemSelected => Theme == AppTheme.System;

    public string CurrentThemeDescription => Theme switch
    {
        AppTheme.Dark => "Always use the dark charcoal interface.",
        AppTheme.Light => "Always use the light interface.",
        _ => "Match the Windows app-mode theme, and update as soon as it changes.",
    };

    public string BackendVersionText { get; } = "scc 4.1.0 (frozen snapshot)";

    public string SnapshotPath { get; } = Path.Combine(
        AppContext.BaseDirectory, "vendor", "scc-source");

    public string DataFolderPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodeScope");

    public string ReportSectionsText => "Overview, language breakdown, ULOC, line-length histogram, " +
                                        "hotspots, change coupling, author statistics, language & author " +
                                        "timelines, COCOMO, LOCOMO, per-file statistics, share card";

    public ICommand ApplyThemeCommand { get; }
    public ICommand OpenDataFolderCommand { get; }
    public ICommand OpenSnapshotDocsCommand { get; }

    private void ApplyTheme(AppTheme theme)
    {
        Theme = theme;
        _themeManager.Apply(theme);
        OnPropertyChanged(nameof(IsDarkSelected));
        OnPropertyChanged(nameof(IsLightSelected));
        OnPropertyChanged(nameof(IsSystemSelected));
        OnPropertyChanged(nameof(CurrentThemeDescription));

        var settings = _store.LoadSettings();
        settings.Theme = theme;
        settings.RememberLastConfiguration = RememberLastConfiguration;
        _store.SaveSettings(settings);
    }

    private void OpenDataFolder()
    {
        try
        {
            _store.EnsureDirectories();
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = _store.RootDirectory,
                UseShellExecute = false,
            });
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            MessageBox.Show($"Could not open the data folder: {ex.Message}", "CodeScope",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenSnapshotDocs()
    {
        var path = Path.Combine(SnapshotPath, "SNAPSHOT.md");
        if (!File.Exists(path))
        {
            MessageBox.Show("The snapshot documentation is missing.", "CodeScope",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            MessageBox.Show($"Could not open the documentation: {ex.Message}", "CodeScope",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>Persist the preferences when the window closes.</summary>
    public void Close()
    {
        var settings = _store.LoadSettings();
        settings.Theme = Theme;
        settings.RememberLastConfiguration = RememberLastConfiguration;
        _store.SaveSettings(settings);
    }
}
