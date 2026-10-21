using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace CodeScope.Models;

/// <summary>Where the report is presented once the analysis finishes.</summary>
public enum AnalysisOutputMode
{
    /// <summary>Render every section with native WPF controls inside the app.</summary>
    InApp,

    /// <summary>Write a self-contained HTML infographic with the frozen engine.</summary>
    Html,
}

/// <summary>One user-toggleable report section.</summary>
public sealed class ReportSectionToggle
{
    [JsonPropertyName("id")]
    public ReportSectionId Id { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    public ReportSectionToggle() { }

    public ReportSectionToggle(ReportSectionId id, bool enabled)
    {
        Id = id;
        Enabled = enabled;
    }

    /// <summary>A short label used when the section cannot be produced.</summary>
    public string AvailabilityNote()
    {
        var info = ReportSectionInfo.For(Id);
        return info.RequiresGit ? "Needs a Git repository." : string.Empty;
    }
}

/// <summary>
/// Everything the user configures on the main screen before running the
/// backend: which sections appear, which analysis options apply, and where the
/// result goes.
/// </summary>
public sealed class ReportConfiguration
{
    [JsonPropertyName("analysis")]
    public AnalysisOptions Analysis { get; set; } = new();

    [JsonPropertyName("sections")]
    public List<ReportSectionToggle> Sections { get; set; } = CreateDefaultSections();

    /// <summary>Title shown in the report banner. Empty means the engine derives it.</summary>
    [JsonPropertyName("reportTitle")]
    public string ReportTitle { get; set; } = string.Empty;

    [JsonPropertyName("outputMode")]
    public AnalysisOutputMode OutputMode { get; set; } = AnalysisOutputMode.InApp;

    /// <summary>Directory the HTML report is written to.</summary>
    [JsonPropertyName("outputFolder")]
    public string OutputFolder { get; set; } = string.Empty;

    /// <summary>File name of the HTML report.</summary>
    [JsonPropertyName("outputFileName")]
    public string OutputFileName { get; set; } = string.Empty;

    /// <summary>
    /// Set only after the user has explicitly agreed to replace an existing
    /// report. The export refuses to write without it, so nothing is ever
    /// overwritten silently.
    /// </summary>
    [JsonIgnore]
    public bool AllowOverwriteReport { get; set; }

    /// <summary>
    /// The default selection reproduces the complete infographic scc produces:
    /// every optional section on, core sections always present.
    /// </summary>
    public static List<ReportSectionToggle> CreateDefaultSections()
    {
        var result = new List<ReportSectionToggle>();
        foreach (var info in ReportSectionInfo.All)
        {
            if (info.IsCore)
            {
                continue;
            }

            result.Add(new ReportSectionToggle(info.Id, enabled: true));
        }

        return result;
    }

    public bool IsEnabled(ReportSectionId id)
    {
        foreach (var toggle in Sections)
        {
            if (toggle.Id == id)
            {
                return toggle.Enabled;
            }
        }

        return true;
    }

    public void SetEnabled(ReportSectionId id, bool enabled)
    {
        foreach (var toggle in Sections)
        {
            if (toggle.Id == id)
            {
                toggle.Enabled = enabled;
                return;
            }
        }
    }

    /// <summary>
    /// The comma-separated section names to hand to <c>--report-skip</c>, i.e.
    /// every optional section the user turned off. This is the exact value the
    /// backend documents for the flag.
    /// </summary>
    public string BuildReportSkipValue()
    {
        var disabled = new List<string>();
        foreach (var toggle in Sections)
        {
            if (toggle.Enabled)
            {
                continue;
            }

            var info = ReportSectionInfo.For(toggle.Id);
            if (info.SccSkipName != null)
            {
                disabled.Add(info.SccSkipName);
            }
        }

        return string.Join(",", disabled);
    }

    public ReportConfiguration Clone()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(this);
        return System.Text.Json.JsonSerializer.Deserialize<ReportConfiguration>(json)!;
    }
}
