using System;
using System.Collections.Generic;
using CodeScope.Models;

namespace CodeScope.ViewModels;

/// <summary>
/// The ready-made presets. Every one of them only combines switches the
/// pinned backend actually supports.
/// </summary>
public static class PresetCatalog
{
    public const string FullReportName = "Full Report";
    public const string OverviewName = "Overview";
    public const string CodeQualityName = "Code Quality";
    public const string GitInsightsName = "Git Insights";
    public const string CustomName = "Custom";

    public static IReadOnlyList<string> BuiltinNames { get; } = new[]
    {
        FullReportName, OverviewName, CodeQualityName, GitInsightsName, CustomName,
    };

    public static bool IsBuiltin(string name) => BuiltinNames.Contains(name);

    public static bool TryGetBuiltin(string name, out ReportConfiguration configuration)
    {
        switch (name)
        {
            case FullReportName:
                configuration = FullReport();
                return true;
            case OverviewName:
                configuration = Overview();
                return true;
            case CodeQualityName:
                configuration = CodeQuality();
                return true;
            case GitInsightsName:
                configuration = GitInsights();
                return true;
            case CustomName:
                configuration = new ReportConfiguration();
                return true;
            default:
                configuration = new ReportConfiguration();
                return false;
        }
    }

    /// <summary>Everything on — the complete infographic.</summary>
    public static ReportConfiguration FullReport() => new();

    /// <summary>Just the summary: totals and the language rollup.</summary>
    public static ReportConfiguration Overview()
    {
        var config = new ReportConfiguration();
        DisableAll(config);

        // Overview keeps the two core sections (always present anyway) plus the
        // unique-lines metric, which is the only extra the summary benefits from.
        config.SetEnabled(ReportSectionId.UniqueLines, true);
        return config;
    }

    /// <summary>Complexity, hotspots, line lengths and file statistics.</summary>
    public static ReportConfiguration CodeQuality()
    {
        var config = new ReportConfiguration();
        DisableAll(config);

        config.SetEnabled(ReportSectionId.UniqueLines, true);
        config.SetEnabled(ReportSectionId.LineLength, true);
        config.SetEnabled(ReportSectionId.Hotspots, true);
        config.SetEnabled(ReportSectionId.Files, true);

        config.Analysis.ComputeCognitive = true;
        config.Analysis.DetectMinifiedOrGenerated = true;
        config.Analysis.ExcludeMinifiedOrGenerated = true;
        config.Analysis.RemoveDuplicates = true;
        config.Analysis.PerFileOutput = true;
        return config;
    }

    /// <summary>Authors, coupling and the timelines.</summary>
    public static ReportConfiguration GitInsights()
    {
        var config = new ReportConfiguration();
        DisableAll(config);

        config.SetEnabled(ReportSectionId.Hotspots, true);
        config.SetEnabled(ReportSectionId.Coupling, true);
        config.SetEnabled(ReportSectionId.Authors, true);
        config.SetEnabled(ReportSectionId.Timeline, true);

        config.Analysis.HistoryDepth = 0;      // the whole history
        config.Analysis.TimelineBuckets = 60;
        config.Analysis.PerFileOutput = false;
        return config;
    }

    private static void DisableAll(ReportConfiguration config)
    {
        foreach (var toggle in config.Sections)
        {
            toggle.Enabled = false;
        }
    }
}
