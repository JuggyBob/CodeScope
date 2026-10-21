using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace CodeScope.Services;

/// <summary>
/// Paints the Windows title bar (the caption, its text and the border) with the
/// application's own theme colours. WPF windows always get a light system
/// caption unless the desktop window manager is told otherwise, which looks
/// broken next to a dark client area; this is that conversation.
/// </summary>
public static class WindowTheme
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;

    /// <summary>Windows 11's "round" corner preference.</summary>
    private const int DwmWindowCornerPreferenceRound = 2;

    /// <summary>
    /// Keep the caption in sync from the moment the window exists. Safe to call
    /// from a constructor: the handle may not exist yet, so it also hooks the
    /// events that fire once it does.
    /// </summary>
    public static void Attach(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.SourceInitialized += (_, _) => Apply(window);
        window.Loaded += (_, _) => Apply(window);
    }

    /// <summary>Re-theme every open window after the app theme changes.</summary>
    public static void RefreshAll()
    {
        if (Application.Current == null)
        {
            return;
        }

        foreach (Window window in Application.Current.Windows)
        {
            Apply(window);
        }
    }

    /// <summary>Apply the current brush set to one window's non-client area.</summary>
    public static void Apply(Window window)
    {
        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            var dark = IsDarkTheme() ? 1 : 0;

            // Windows 10 1903+ understands attribute 20; the 1809 builds use 19.
            if (DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkModeBefore20H1, ref dark, sizeof(int));
            }

            // Windows 11 lets us pick the exact caption colours so the title bar
            // blends with the header instead of merely being "dark".
            if (TryGetColor("SurfaceBrush", out var caption))
            {
                DwmSetWindowAttribute(handle, DwmwaCaptionColor, ref caption, sizeof(int));
            }

            if (TryGetColor("TextPrimaryBrush", out var text))
            {
                DwmSetWindowAttribute(handle, DwmwaTextColor, ref text, sizeof(int));
            }

            if (TryGetColor("BorderBrush", out var border))
            {
                DwmSetWindowAttribute(handle, DwmwaBorderColor, ref border, sizeof(int));
            }

            // Windows 11 rounds the window itself; older builds simply ignore
            // the attribute, which is why the call's result is not checked.
            var corners = DwmWindowCornerPreferenceRound;
            DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref corners, sizeof(int));
        }
        catch
        {
            // A missing DWM (remote sessions, older Windows) is not fatal.
        }
    }

    private static bool IsDarkTheme()
    {
        if (Application.Current?.TryFindResource("BgBrush") is SolidColorBrush brush)
        {
            var c = brush.Color;
            return (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) < 128;
        }

        return true;
    }

    /// <summary>Read a theme brush and convert it to a Win32 COLORREF.</summary>
    private static bool TryGetColor(string key, out int colorRef)
    {
        colorRef = 0;
        if (Application.Current?.TryFindResource(key) is not SolidColorBrush brush)
        {
            return false;
        }

        var c = brush.Color;
        colorRef = c.R | (c.G << 8) | (c.B << 16);
        return true;
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref int attributeValue,
        int attributeSize);
}
