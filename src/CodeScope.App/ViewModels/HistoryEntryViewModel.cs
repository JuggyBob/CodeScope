using System;
using System.Globalization;
using CodeScope.Models;

namespace CodeScope.ViewModels;

/// <summary>
/// One row in the history sidebar: what was analysed, when, how big it was and
/// how long it took. The heavy report data stays in the store; this is the
/// summary the user reads.
/// </summary>
public sealed class HistoryEntryViewModel
{
    private readonly HistoryEntry _entry;

    public HistoryEntryViewModel(HistoryEntry entry)
    {
        _entry = entry;
    }

    public HistoryEntry Entry => _entry;

    public string Id => _entry.Id;

    public string ProjectName =>
        _entry.ProjectName.Length > 0 ? _entry.ProjectName : "(unknown folder)";

    /// <summary>
    /// The row's accessible name. Without this, assistive technology would
    /// announce the type name instead of the project.
    /// </summary>
    public override string ToString() => ProjectName;

    public string ProjectPath => _entry.ProjectPath;

    /// <summary>Where the HTML report was written, for HTML-mode runs.</summary>
    public string HtmlPath => _entry.HtmlPath ?? string.Empty;

    /// <summary>"just now", "12 minutes ago", "3 days ago"…</summary>
    public string WhenText => DescribeTime(_entry.Timestamp);

    /// <summary>The absolute time, for the tooltip.</summary>
    public string FullTime => _entry.Timestamp.ToLocalTime().ToString("f", CultureInfo.CurrentCulture);

    /// <summary>"140 files · 23,413 lines"</summary>
    public string MetricsText
    {
        get
        {
            if (_entry.Files <= 0 && _entry.Lines <= 0)
            {
                return "no files counted";
            }

            return _entry.Files.ToString("N0", CultureInfo.CurrentCulture) + " files · " +
                   _entry.Lines.ToString("N0", CultureInfo.CurrentCulture) + " lines";
        }
    }

    /// <summary>"0.52 s" or "1.4 min"</summary>
    public string DurationText
    {
        get
        {
            var seconds = _entry.DurationMs / 1000d;
            return seconds < 60
                ? seconds.ToString("0.00", CultureInfo.CurrentCulture) + " s"
                : (seconds / 60).ToString("0.0", CultureInfo.CurrentCulture) + " min";
        }
    }

    /// <summary>"In-app" or "HTML" — which output mode produced the run.</summary>
    public string ModeText => _entry.Mode == AnalysisOutputMode.Html ? "HTML" : "In-app";

    public bool IsHtml => _entry.Mode == AnalysisOutputMode.Html;

    /// <summary>True when the run can be reopened in the dashboard.</summary>
    public bool CanReopen => _entry.Data != null;

    /// <summary>The name of the preset that was applied, if any.</summary>
    public string PresetName => _entry.PresetName;

    private static string DescribeTime(DateTimeOffset timestamp)
    {
        var delta = DateTimeOffset.Now - timestamp;

        if (delta < TimeSpan.FromMinutes(1))
        {
            return "just now";
        }

        if (delta < TimeSpan.FromHours(1))
        {
            var minutes = (int)delta.TotalMinutes;
            return minutes == 1 ? "1 minute ago" : minutes + " minutes ago";
        }

        if (delta < TimeSpan.FromDays(1))
        {
            var hours = (int)delta.TotalHours;
            return hours == 1 ? "1 hour ago" : hours + " hours ago";
        }

        var days = (int)delta.TotalDays;
        if (days == 1)
        {
            return "yesterday";
        }

        if (days < 7)
        {
            return days + " days ago";
        }

        return timestamp.ToLocalTime().ToString("d MMM", CultureInfo.CurrentCulture);
    }
}
