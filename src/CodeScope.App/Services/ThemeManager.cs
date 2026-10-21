using System;
using System.IO;
using System.Security;
using System.Timers;
using System.Windows;
using Microsoft.Win32;
using CodeScope.Models;

namespace CodeScope.Services;

/// <summary>
/// Owns the three-way theme switch (Dark / Light / Follow System) and applies it
/// by swapping the merged theme resource dictionary, so every brush and style in
/// the application — including dialogs and the report view — follows the choice
/// without a restart.
/// </summary>
public sealed class ThemeManager : IDisposable
{
    private const string ThemeDarkDictionary = "Resources/Themes/Dark.xaml";
    private const string ThemeLightDictionary = "Resources/Themes/Light.xaml";

    private static readonly string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private readonly System.Timers.Timer _pollTimer;
    private bool _systemLight = false;
    private bool _disposed;

    public ThemeManager()
    {
        _pollTimer = new System.Timers.Timer(2000);
        _pollTimer.Elapsed += OnPoll;
        _pollTimer.AutoReset = true;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        _systemLight = DetectSystemLightTheme();
    }

    /// <summary>The theme that is actually on screen right now.</summary>
    public AppTheme EffectiveTheme(AppTheme requested) =>
        requested == AppTheme.System ? (_systemLight ? AppTheme.Light : AppTheme.Dark) : requested;

    /// <summary>The choice the user made (Dark, Light or Follow System).</summary>
    public AppTheme RequestedTheme { get; private set; } = AppTheme.Dark;

    public bool IsSystemLight => _systemLight;

    /// <summary>Detect the current Windows app theme (light vs dark).</summary>
    public bool DetectSystemLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int light)
            {
                return light != 0;
            }

            // Fall back to the newer Windows 11 registry location.
            using var key2 = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            if (key2?.GetValue("SystemUsesLightTheme") is int systemLight)
            {
                return systemLight != 0;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            // No registry access: assume dark, which is the Windows default.
        }

        return false;
    }

    /// <summary>Apply the requested theme immediately.</summary>
    public void Apply(AppTheme theme)
    {
        if (Application.Current == null)
        {
            return;
        }

        RequestedTheme = theme;

        var effective = EffectiveTheme(theme);
        var source = effective == AppTheme.Light
            ? new Uri(ThemeLightDictionary, UriKind.Relative)
            : new Uri(ThemeDarkDictionary, UriKind.Relative);

        var dictionaries = Application.Current.Resources.MergedDictionaries;
        for (var i = 0; i < dictionaries.Count; i++)
        {
            if (dictionaries[i].Source != null &&
                (dictionaries[i].Source.OriginalString.EndsWith("Dark.xaml", StringComparison.OrdinalIgnoreCase) ||
                 dictionaries[i].Source.OriginalString.EndsWith("Light.xaml", StringComparison.OrdinalIgnoreCase)))
            {
                dictionaries.RemoveAt(i);
                break;
            }
        }

        dictionaries.Insert(0, new ResourceDictionary { Source = source });
        _pollTimer.Enabled = theme == AppTheme.System;

        // The Windows caption lives outside WPF's resource system; repaint it here.
        WindowTheme.RefreshAll();
    }

    /// <summary>Re-read the OS theme; returns true when the effective theme changed.</summary>
    public bool RefreshSystemTheme()
    {
        var light = DetectSystemLightTheme();
        if (light == _systemLight)
        {
            return false;
        }

        _systemLight = light;
        return true;
    }

    private void OnPoll(object? sender, ElapsedEventArgs e)
    {
        if (RequestedTheme != AppTheme.System || Application.Current == null)
        {
            return;
        }

        if (RefreshSystemTheme())
        {
            // Cross-thread: marshal the dictionary swap back to the UI thread.
            Application.Current.Dispatcher.BeginInvoke(() => Apply(AppTheme.System));
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        // The system theme may have flipped; re-apply so the switch is instant.
        if (Application.Current == null)
        {
            RefreshSystemTheme();
            return;
        }

        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (RefreshSystemTheme() && RequestedTheme == AppTheme.System)
            {
                Apply(AppTheme.System);
            }
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _pollTimer.Stop();
        _pollTimer.Dispose();
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }
}
