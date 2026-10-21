using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CodeScope.Models;

/// <summary>The three theme choices offered on the Settings page.</summary>
public enum AppTheme
{
    Dark,
    Light,
    System,
}

/// <summary>
/// Preferences that survive between launches. Stored as JSON in the per-user
/// application-data folder — never inside the analysed project.
/// </summary>
public sealed class AppSettings
{
    [JsonPropertyName("theme")]
    public AppTheme Theme { get; set; } = AppTheme.Dark;

    [JsonPropertyName("lastProjectFolder")]
    public string? LastProjectFolder { get; set; }

    [JsonPropertyName("lastOutputFolder")]
    public string? LastOutputFolder { get; set; }

    [JsonPropertyName("lastReportTitle")]
    public string? LastReportTitle { get; set; }

    [JsonPropertyName("lastReportFileName")]
    public string? LastReportFileName { get; set; }

    [JsonPropertyName("lastPresetName")]
    public string? LastPresetName { get; set; }

    /// <summary>Names of the user's own presets, in save order.</summary>
    [JsonPropertyName("customPresets")]
    public List<string> CustomPresets { get; set; } = new();

    [JsonPropertyName("lastSelectedSectionSet")]
    public string? LastSelectedSectionSet { get; set; }

    [JsonPropertyName("rememberLastConfiguration")]
    public bool RememberLastConfiguration { get; set; } = true;

    /// <summary>True once the first-run walkthrough has been finished or skipped.</summary>
    [JsonPropertyName("onboardingCompleted")]
    public bool OnboardingCompleted { get; set; }

    /// <summary>True when the history sidebar is open.</summary>
    [JsonPropertyName("sidebarExpanded")]
    public bool SidebarExpanded { get; set; } = true;

    public AppSettings Clone()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(this);
        return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json)!;
    }
}
