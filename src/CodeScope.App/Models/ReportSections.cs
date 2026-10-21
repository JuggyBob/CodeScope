using System;
using System.Collections.Generic;

namespace CodeScope.Models;

/// <summary>
/// Identifiers for every section the pinned scc backend can produce.
/// </summary>
public enum ReportSectionId
{
    /// <summary>Headline totals strip (files / lines / SLOC / complexity / bytes).</summary>
    Overview,

    /// <summary>Per-language rollup table.</summary>
    LanguageBreakdown,

    /// <summary>Unique Lines of Code and DRYness.</summary>
    UniqueLines,

    /// <summary>Line-length histogram and longest-line outliers.</summary>
    LineLength,

    /// <summary>Code hotspots ranked by complexity x change frequency.</summary>
    Hotspots,

    /// <summary>Change coupling between file pairs.</summary>
    Coupling,

    /// <summary>Author rollup with bus factor.</summary>
    Authors,

    /// <summary>Language and author timelines.</summary>
    Timeline,

    /// <summary>COCOMO cost estimate.</summary>
    Cocomo,

    /// <summary>LOCOMO (LLM regeneration) cost estimate.</summary>
    Locomo,

    /// <summary>Per-file statistics table.</summary>
    Files,

    /// <summary>OpenGraph / Twitter share card (og:image).</summary>
    ShareCard,
}

/// <summary>
/// Describes one report section as the UI should present it.
/// </summary>
public sealed class ReportSectionInfo
{
    private ReportSectionInfo(ReportSectionId id, string name, string description, bool isCore,
        bool requiresGit, string? sccSkipName)
    {
        Id = id;
        Name = name;
        Description = description;
        IsCore = isCore;
        RequiresGit = requiresGit;
        SccSkipName = sccSkipName;
    }

    public ReportSectionId Id { get; }

    public string Name { get; }

    /// <summary>One-line explanation shown under the toggle and in the tooltip.</summary>
    public string Description { get; }

    /// <summary>
    /// Core sections are part of every scc infographic; the backend has no
    /// switch to omit them, so they are not user-toggleable.
    /// </summary>
    public bool IsCore { get; }

    /// <summary>True when the section needs a Git repository to be produced at all.</summary>
    public bool RequiresGit { get; }

    /// <summary>The value to pass to <c>--report-skip</c> to omit this section.</summary>
    public string? SccSkipName { get; }

    /// <summary>
    /// The complete section catalogue, in report order. Optional sections
    /// default to enabled, matching scc's own complete infographic.
    /// </summary>
    public static IReadOnlyList<ReportSectionInfo> All { get; } = new List<ReportSectionInfo>
    {
        new ReportSectionInfo(ReportSectionId.Overview, "Overview & Summary Metrics",
            "Headline totals: files, lines, code, comments, blanks, complexity and bytes.",
            isCore: true, requiresGit: false, sccSkipName: null),

        new ReportSectionInfo(ReportSectionId.LanguageBreakdown, "Language Breakdown",
            "Per-language statistics: files, lines, code, comment, blank, complexity and share.",
            isCore: true, requiresGit: false, sccSkipName: null),

        new ReportSectionInfo(ReportSectionId.UniqueLines, "Unique Lines of Code",
            "ULOC counts plus DRYness (unique lines / total lines).",
            isCore: false, requiresGit: false, sccSkipName: "uloc"),

        new ReportSectionInfo(ReportSectionId.LineLength, "Line-Length Histogram",
            "Distribution of line lengths and the ten longest lines in the codebase.",
            isCore: false, requiresGit: false, sccSkipName: "linelength"),

        new ReportSectionInfo(ReportSectionId.Hotspots, "Code Hotspots",
            "Files ranked by complexity x change frequency over recent Git history.",
            isCore: false, requiresGit: true, sccSkipName: "hotspots"),

        new ReportSectionInfo(ReportSectionId.Coupling, "Change Coupling",
            "File pairs that change together over recent Git history.",
            isCore: false, requiresGit: true, sccSkipName: "coupling"),

        new ReportSectionInfo(ReportSectionId.Authors, "Author Statistics",
            "Per-author rollup: code owned, files touched, bus factor and coverage.",
            isCore: false, requiresGit: true, sccSkipName: "authors"),

        new ReportSectionInfo(ReportSectionId.Timeline, "Language & Author Timelines",
            "Code volume per language and per author across the commit window.",
            isCore: false, requiresGit: true, sccSkipName: "timeline"),

        new ReportSectionInfo(ReportSectionId.Cocomo, "COCOMO Estimate",
            "Basic COCOMO estimate of effort, schedule and cost to develop from scratch.",
            isCore: false, requiresGit: false, sccSkipName: "cocomo"),

        new ReportSectionInfo(ReportSectionId.Locomo, "LOCOMO Estimate",
            "Estimate of the LLM token cost to regenerate the codebase.",
            isCore: false, requiresGit: false, sccSkipName: "locomo"),

        new ReportSectionInfo(ReportSectionId.Files, "Per-File Statistics",
            "Searchable table with one row per analysed file.",
            isCore: false, requiresGit: false, sccSkipName: "files"),

        new ReportSectionInfo(ReportSectionId.ShareCard, "Share Card",
            "OpenGraph / Twitter preview image embedded as og:image in the HTML report.",
            isCore: false, requiresGit: false, sccSkipName: "card"),
    };

    public static ReportSectionInfo For(ReportSectionId id)
    {
        foreach (var info in All)
        {
            if (info.Id == id)
            {
                return info;
            }
        }

        throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown report section.");
    }

    public static bool TryGet(string sccSkipName, out ReportSectionId id)
    {
        foreach (var info in All)
        {
            if (info.SccSkipName == sccSkipName)
            {
                id = info.Id;
                return true;
            }
        }

        id = default;
        return false;
    }
}
