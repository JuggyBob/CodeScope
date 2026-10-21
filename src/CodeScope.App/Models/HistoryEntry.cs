using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CodeScope.Models;

/// <summary>
/// One finished analysis, kept on disk so the report can be reopened later
/// without running the engine again. The record lives in the per-user
/// application-data folder — never inside the analysed project.
/// </summary>
public sealed class HistoryEntry
{
    /// <summary>Stable identifier, also the name of the file on disk.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>When the run finished.</summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("projectPath")]
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>Folder name, shown in the sidebar.</summary>
    [JsonPropertyName("projectName")]
    public string ProjectName { get; set; } = string.Empty;

    [JsonPropertyName("mode")]
    public AnalysisOutputMode Mode { get; set; } = AnalysisOutputMode.InApp;

    /// <summary>The preset that was applied when the run started, if any.</summary>
    [JsonPropertyName("presetName")]
    public string PresetName { get; set; } = string.Empty;

    [JsonPropertyName("reportTitle")]
    public string ReportTitle { get; set; } = string.Empty;

    [JsonPropertyName("files")]
    public long Files { get; set; }

    [JsonPropertyName("lines")]
    public long Lines { get; set; }

    [JsonPropertyName("code")]
    public long Code { get; set; }

    [JsonPropertyName("complexity")]
    public long Complexity { get; set; }

    [JsonPropertyName("durationMs")]
    public long DurationMs { get; set; }

    /// <summary>Where the HTML report was written, for HTML-mode runs.</summary>
    [JsonPropertyName("htmlPath")]
    public string? HtmlPath { get; set; }

    /// <summary>
    /// The optional sections that were turned off for this run. Keeping them
    /// lets a past run be reopened exactly as it was produced.
    /// </summary>
    [JsonPropertyName("disabledSections")]
    public List<string> DisabledSections { get; set; } = new();

    /// <summary>
    /// The engine's own report data. Keeping it means a past run can be
    /// reopened in the dashboard exactly as it was produced.
    /// </summary>
    [JsonPropertyName("data")]
    public SccReportData? Data { get; set; }
}
