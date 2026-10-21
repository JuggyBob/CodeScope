using System;
using System.Globalization;

namespace CodeScope.Services;

/// <summary>
/// Display-only formatting helpers. These never feed back into the analysis;
/// the engine owns every number.
/// </summary>
public static class Format
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>12,345</summary>
    public static string Number(long value) => value.ToString("N0", Inv);

    /// <summary>12,345</summary>
    public static string Number(double value) => value.ToString("N0", Inv);

    /// <summary>48,599</summary>
    public static string Int64(long value) => value.ToString("N0", Inv);

    /// <summary>1.2 MB / 34.2 kB, picking a unit that keeps the number readable.</summary>
    public static string Bytes(long bytes, string unit)
    {
        if (bytes == 0)
        {
            return "0 B";
        }

        double value;
        string suffix;
        switch (unit.ToLowerInvariant())
        {
            case "binary":
                (value, suffix) = Scale(bytes, 1024d, new[] { "B", "KiB", "MiB", "GiB", "TiB" });
                break;
            case "xkcd-kb":
                (value, suffix) = Scale(bytes, 1024d, new[] { "B", "kB", "MB", "GB" });
                break;
            case "xkcd-kelly":
                (value, suffix) = Scale(bytes, 1000d, new[] { "B", "kB", "MB", "GB" });
                break;
            case "xkcd-imaginary":
                (value, suffix) = Scale(bytes, 1024d, new[] { "B", "kiB", "MiB", "GiB" });
                break;
            case "xkcd-intel":
                (value, suffix) = Scale(bytes, 1024d, new[] { "B", "KB", "MB", "GB" });
                break;
            case "xkcd-drive":
                (value, suffix) = Scale(bytes, 1000d, new[] { "B", "KB", "MB", "GB" });
                break;
            case "xkcd-bakers":
                (value, suffix) = Scale(bytes, 1000d, new[] { "B", "kB", "MB", "GB", "dB" });
                break;
            case "mixed":
                (value, suffix) = Scale(bytes, 1000d, new[] { "B", "kB", "MB", "GB", "TB", "PB", "XB" });
                break;
            case "si":
            default:
                (value, suffix) = Scale(bytes, 1000d, new[] { "B", "kB", "MB", "GB", "TB", "PB" });
                break;
        }

        return string.Format(Inv, "{0:0.##} {1}", value, suffix);
    }

    private static (double value, string suffix) Scale(double bytes, double step, string[] units)
    {
        double value = bytes;
        int index = 0;
        while (value >= step && index < units.Length - 1)
        {
            value /= step;
            index++;
        }

        return (value, units[index]);
    }

    /// <summary>14.2%</summary>
    public static string Percent(double value) => value.ToString("0.0%", Inv);

    /// <summary>38.6%</summary>
    public static string Percent1(double value) => value.ToString("0.0", Inv) + "%";

    /// <summary>$1,594,411</summary>
    public static string Currency(double amount, string symbol) =>
        symbol + Math.Round(amount).ToString("N0", Inv);

    /// <summary>$1,594,411.15</summary>
    public static string CurrencyExact(double amount, string symbol) =>
        symbol + amount.ToString("N2", Inv);

    /// <summary>141.6</summary>
    public static string Fixed1(double value) => value.ToString("0.0", Inv);

    /// <summary>16.42</summary>
    public static string Fixed2(double value) => value.ToString("0.00", Inv);

    /// <summary>1.2 M / 342 k / 950</summary>
    public static string Compact(double value)
    {
        if (value >= 1_000_000_000d)
        {
            return (value / 1_000_000_000d).ToString("0.0", Inv) + " B";
        }

        if (value >= 1_000_000d)
        {
            return (value / 1_000_000d).ToString("0.0", Inv) + " M";
        }

        if (value >= 1_000d)
        {
            return (value / 1_000d).ToString("0.0", Inv) + " k";
        }

        return value.ToString("0.##", Inv);
    }

    /// <summary>1,000 h 48 m / 950 h</summary>
    public static string Duration(double hours)
    {
        if (hours < 1)
        {
            return Math.Round(hours * 60).ToString("N0", Inv) + " min";
        }

        if (hours < 100)
        {
            return hours.ToString("0.0", Inv) + " hr";
        }

        long totalMinutes = (long)Math.Round(hours * 60);
        long h = totalMinutes / 60;
        long m = totalMinutes % 60;
        return m == 0
            ? h.ToString("N0", Inv) + " hr"
            : string.Format(Inv, "{0} h {1} min", h.ToString("N0"), m);
    }

    /// <summary>21,777 s</summary>
    public static string Seconds(double seconds)
    {
        if (seconds < 60)
        {
            return seconds.ToString("0.0", Inv) + " s";
        }

        return Duration(seconds / 3600d);
    }

    /// <summary>2026-10-18 14:42</summary>
    public static string DateTime(string? iso8601)
    {
        if (string.IsNullOrWhiteSpace(iso8601) || !DateTimeOffset.TryParse(iso8601, out var value))
        {
            return string.Empty;
        }

        return value.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
    }

    /// <summary>8.1 h</summary>
    public static string ReviewHours(double hours) => Duration(hours);

    /// <summary>
    /// The moment a commit was written, so a column can be ordered by time
    /// rather than by its printed form. The epoch stands for "not known".
    /// </summary>
    public static DateTimeOffset CommitDate(string? iso8601) =>
        !string.IsNullOrWhiteSpace(iso8601) && DateTimeOffset.TryParse(iso8601, out var value)
            ? value
            : DateTimeOffset.MinValue;
}
