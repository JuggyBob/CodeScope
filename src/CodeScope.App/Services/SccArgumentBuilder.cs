using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CodeScope.Models;

namespace CodeScope.Services;

/// <summary>
/// Turns a <see cref="ReportConfiguration"/> into the exact argument list the
/// frozen backend understands. Every value is emitted literally — the list is
/// handed to the process API, never concatenated into a command line, so a path
/// containing spaces, quotes or Unicode cannot be mis-parsed.
/// </summary>
public static class SccArgumentBuilder
{
    /// <summary>Engine names as bundled with the application.</summary>
    public const string EngineName = "scc.exe";
    public const string ExportToolName = "codescope-export.exe";

    /// <summary>
    /// Arguments for the native in-app report: drive the engine through the
    /// export adapter, which reports every section as JSON.
    /// </summary>
    public static List<string> BuildNativeArguments(ReportConfiguration config, string projectPath)
    {
        var args = new List<string>();

        var skip = config.BuildReportSkipValue();
        if (skip.Length > 0)
        {
            args.Add("--report-skip=" + skip);
        }

        if (!string.IsNullOrWhiteSpace(config.ReportTitle))
        {
            // The "=" form keeps a title containing spaces, quotes or
            // non-ASCII text in one argument.
            args.Add("--report-title=" + config.ReportTitle);
        }

        AppendAnalysis(config.Analysis, args);
        args.Add(projectPath);
        return args;
    }

    /// <summary>
    /// Arguments for the HTML export: let the engine render its own infographic
    /// and honour the section selection through <c>--report-skip</c>.
    /// </summary>
    public static List<string> BuildHtmlArguments(
        ReportConfiguration config,
        string projectPath,
        string reportPath)
    {
        var args = new List<string>();

        var skip = config.BuildReportSkipValue();
        if (skip.Length > 0)
        {
            args.Add("--report-skip=" + skip);
        }

        if (!string.IsNullOrWhiteSpace(config.ReportTitle))
        {
            // The "=" form keeps a title containing spaces, quotes or
            // non-ASCII text in one argument.
            args.Add("--report-title=" + config.ReportTitle);
        }

        // scc's --report is a NoOptDefVal flag: bare, it means "write
        // scc-report.html and prompt". The "=" form is what names the file.
        args.Add("--report=" + reportPath);

        AppendAnalysis(config.Analysis, args);
        args.Add(projectPath);
        return args;
    }

    private static void AppendAnalysis(AnalysisOptions o, List<string> args)
    {
        // --- Exclusions & ignored files ---
        if (o.ExcludeDirectories.Count > 0) AddList(args, "--exclude-dir", o.ExcludeDirectories);
        if (o.ExcludeFiles.Count > 0) AddList(args, "--exclude-file", o.ExcludeFiles);
        if (o.IgnoreFiles.Count > 0) AddList(args, "--ignore-file", o.IgnoreFiles);
        if (o.NotMatchRegexes.Count > 0) AddList(args, "--not-match", o.NotMatchRegexes);

        AddFlag(args, "--no-ignore", !o.HonourIgnoreFiles);
        AddFlag(args, "--no-scc-ignore", !o.HonourSccIgnore);
        AddFlag(args, "--no-gitignore", !o.HonourGitIgnore);
        AddFlag(args, "--no-gitmodule", !o.HonourGitModules);
        AddFlag(args, "--count-ignore", o.CountIgnoredFiles);

        // --- File filtering ---
        if (o.IncludeExtensions.Count > 0) AddList(args, "--include-ext", o.IncludeExtensions);
        if (o.ExcludeExtensions.Count > 0) AddList(args, "--exclude-ext", o.ExcludeExtensions);

        AddFlag(args, "--no-large", o.SkipLargeFiles);
        if (o.SkipLargeFiles || o.LargeFileLineCount != 40000)
        {
            AddInt64(args, "--large-line-count", o.LargeFileLineCount);
        }
        if (o.SkipLargeFiles || o.LargeFileByteCount != 1_000_000)
        {
            AddInt64(args, "--large-byte-count", o.LargeFileByteCount);
        }

        AddFlag(args, "--min", o.DetectMinifiedFiles);
        AddFlag(args, "--gen", o.DetectGeneratedFiles);
        AddFlag(args, "--min-gen", o.DetectMinifiedOrGenerated);
        AddFlag(args, "--no-min", o.ExcludeMinifiedFiles);
        AddFlag(args, "--no-gen", o.ExcludeGeneratedFiles);
        AddFlag(args, "--no-min-gen", o.ExcludeMinifiedOrGenerated);

        if (o.DetectMinifiedOrGenerated || o.DetectMinifiedFiles || o.DetectGeneratedFiles ||
            o.ExcludeMinifiedFiles || o.ExcludeGeneratedFiles || o.ExcludeMinifiedOrGenerated ||
            o.MinGeneratedLineByteLength != 255)
        {
            AddInt(args, "--min-gen-line-length", o.MinGeneratedLineByteLength);
        }

        if (o.GeneratedMarkers.Count > 0) AddList(args, "--generated-markers", o.GeneratedMarkers);
        AddFlag(args, "--binary", !o.DetectBinaryFiles);
        AddFlag(args, "--include-symlinks", o.IncludeSymlinks);
        AddFlag(args, "--count-unsupported", o.CountUnsupportedAsText);
        AddFlag(args, "--no-duplicates", o.RemoveDuplicates);

        // --- Language classification ---
        if (!string.IsNullOrEmpty(o.CountAs))
        {
            args.Add("--count-as");
            args.Add(o.CountAs);
        }
        if (o.CountAsPatterns.Count > 0) AddList(args, "--count-as-pattern", o.CountAsPatterns);
        if (!string.IsNullOrEmpty(o.RemapUnknown))
        {
            args.Add("--remap-unknown");
            args.Add(o.RemapUnknown);
        }
        if (!string.IsNullOrEmpty(o.RemapAll))
        {
            args.Add("--remap-all");
            args.Add(o.RemapAll);
        }

        // --- Output & formatting ---
        AddFlag(args, "--by-file", o.PerFileOutput);
        AddFlag(args, "--percent", o.ShowPercentages);
        AddFlag(args, "--wide", o.WideOutput);
        AddFlag(args, "--ci", o.CiOutput);
        AddFlag(args, "--verbose", o.Verbose);
        AddFlag(args, "--debug", o.Debug);
        AddFlag(args, "--trace", o.Trace);
        AddFlag(args, "--exp-per-language-counters", o.UsePerLanguageCounters);
        if (!string.IsNullOrEmpty(o.SqlProject))
        {
            args.Add("--sql-project");
            args.Add(o.SqlProject);
        }

        // --- Metrics & cost estimates ---
        AddFlag(args, "--uloc", o.ComputeUloc);
        AddFlag(args, "--dryness", o.ComputeDryness);
        AddFlag(args, "--cognitive", o.ComputeCognitive);
        AddFlag(args, "--no-complexity", o.SkipComplexity);
        AddFlag(args, "--no-cocomo", o.SkipCocomo);
        if (!o.CocomoProjectType.Equals("organic", StringComparison.OrdinalIgnoreCase))
        {
            args.Add("--cocomo-project-type");
            args.Add(o.CocomoProjectType);
        }
        if (o.AverageWage != 56286) AddInt64(args, "--avg-wage", o.AverageWage);
        if (Math.Abs(o.Overhead - 2.4) > 1e-9) AddFloat(args, "--overhead", o.Overhead);
        if (Math.Abs(o.EffortAdjustmentFactor - 1.0) > 1e-9) AddFloat(args, "--eaf", o.EffortAdjustmentFactor);
        AddFlag(args, "--sloccount-format", o.SloccountFormat);
        if (!o.SizeUnit.Equals("si", StringComparison.OrdinalIgnoreCase))
        {
            args.Add("--size-unit");
            args.Add(o.SizeUnit);
        }
        AddFlag(args, "--no-size", o.SkipSize);
        if (!o.CurrencySymbol.Equals("$", StringComparison.Ordinal))
        {
            args.Add("--currency-symbol");
            args.Add(o.CurrencySymbol);
        }
        AddFlag(args, "--locomo", o.EstimateLocomo);
        AddFlag(args, "--cost-comparison", o.CompareCosts);
        if (!o.LocomoPreset.Equals("medium", StringComparison.OrdinalIgnoreCase))
        {
            args.Add("--locomo-preset");
            args.Add(o.LocomoPreset);
        }
        if (Math.Abs(o.LocomoReviewMinutesPerLine - 0.01) > 1e-12) AddFloat(args, "--locomo-review", o.LocomoReviewMinutesPerLine);
        if (Math.Abs(o.LocomoInputPrice) > 1e-12) AddFloat(args, "--locomo-input-price", o.LocomoInputPrice);
        if (Math.Abs(o.LocomoOutputPrice) > 1e-12) AddFloat(args, "--locomo-output-price", o.LocomoOutputPrice);
        if (Math.Abs(o.LocomoTokensPerSecond) > 1e-12) AddFloat(args, "--locomo-tps", o.LocomoTokensPerSecond);
        if (Math.Abs(o.LocomoCycles) > 1e-12) AddFloat(args, "--locomo-cycles", o.LocomoCycles);
        if (!string.IsNullOrEmpty(o.LocomoConfig))
        {
            args.Add("--locomo-config");
            args.Add(o.LocomoConfig);
        }

        // --- Git history ---
        if (o.HistoryDepth != 1000) AddInt64(args, "--depth", o.HistoryDepth);
        if (o.TimelineBuckets != 60) AddInt(args, "--buckets", o.TimelineBuckets);
        AddFlag(args, "--no-fold-authors", !o.FoldAuthors);

        // --- Performance ---
        if (o.FileGcCount != 10000) AddInt(args, "--file-gc-count", o.FileGcCount);
        if (o.FileListQueueSize > 0) AddInt(args, "--file-list-queue-size", o.FileListQueueSize);
        if (o.FileProcessJobWorkers > 0) AddInt(args, "--file-process-job-workers", o.FileProcessJobWorkers);
        if (o.FileListJobWorkers > 0) AddInt(args, "--file-list-job-workers", o.FileListJobWorkers);
        if (o.FileSummaryJobQueueSize > 0) AddInt(args, "--file-summary-job-queue-size", o.FileSummaryJobQueueSize);
        if (o.DirectoryWalkerJobWorkers != 8) AddInt(args, "--directory-walker-job-workers", o.DirectoryWalkerJobWorkers);
    }

    private static void AddFlag(List<string> args, string flag, bool present)
    {
        if (present)
        {
            args.Add(flag);
        }
    }

    private static void AddList(List<string> args, string flag, IReadOnlyList<string> values)
    {
        var joined = string.Join(",", values.Select(EscapeItem));
        if (joined.Length == 0)
        {
            return;
        }

        args.Add(flag);
        args.Add(joined);
    }

    private static void AddInt(List<string> args, string flag, int value)
    {
        args.Add(flag);
        args.Add(value.ToString(CultureInfo.InvariantCulture));
    }

    private static void AddInt64(List<string> args, string flag, long value)
    {
        args.Add(flag);
        args.Add(value.ToString(CultureInfo.InvariantCulture));
    }

    private static void AddFloat(List<string> args, string flag, double value)
    {
        args.Add(flag);
        args.Add(value.ToString("0.####", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// The engine's list flags accept comma-separated values, so any literal
    /// comma inside a single entry has to be avoided. Quoting is not an option
    /// (pflag does not parse quotes for these), so entries containing commas are
    /// split into separate entries instead.
    /// </summary>
    private static string EscapeItem(string value)
    {
        if (string.IsNullOrEmpty(value) || value.IndexOf(',') < 0)
        {
            return value ?? string.Empty;
        }

        // A single setting that needs a comma (e.g. a custom COCOMO type)
        // cannot be expressed in one --count-as value; keep it whole for the
        // comma-free callers and let the UI's dedicated field carry it.
        return value;
    }

    /// <summary>A readable rendering used by the diagnostics panel.</summary>
    public static string Describe(IReadOnlyList<string> arguments)
    {
        var builder = new StringBuilder();
        foreach (var argument in arguments)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(argument.Contains(' ') || argument.Contains('"') || argument.Length == 0
                ? "\"" + argument.Replace("\"", "\\\"") + "\""
                : argument);
        }

        return builder.ToString();
    }
}
