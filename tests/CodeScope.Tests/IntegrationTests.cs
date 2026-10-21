using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using System.Text.Json;
using CodeScope.Models;
using CodeScope.Services;

namespace CodeScope.Tests;

/// <summary>
/// End-to-end checks against the two executables in backend\. These do not mock
/// the engine: they analyse real trees, including a real Git repository, and
/// compare the results with what the CLI itself reports.
/// </summary>
public static class IntegrationTests
{
    public static void Run(TestHarness h)
    {
        var backendRoot = AppContext.BaseDirectory;
        var engine = Path.Combine(backendRoot, "backend", "scc.exe");
        var adapter = Path.Combine(backendRoot, "backend", "codescope-export.exe");

        if (!File.Exists(engine) || !File.Exists(adapter))
        {
            Console.WriteLine();
            Console.WriteLine("  SKIP  integration tests — backend executables are missing.");
            Console.WriteLine($"        expected: {engine}");
            Console.WriteLine("        run build\\build-backend.ps1 to produce them");
            return;
        }

        var workspace = Fixtures.CreateWorkspace();
        try
        {
            Console.WriteLine();
            Console.WriteLine($"  workspace: {workspace}");

            var plain = Fixtures.CreatePlainProject(workspace);
            var isolated = Fixtures.CreateIsolatedProject(workspace);
            var git = Fixtures.CreateGitProject(workspace);

            Backend(h, engine, adapter);
            BackendFlagSurface(h, adapter);
            NativeMode(h, adapter, plain);
            GitSections(h, adapter, git, isolated);
            SectionSelection(h, adapter, plain);
            HtmlExport(h, engine, adapter, plain, git);
            NoHtmlInNativeMode(h, adapter, plain);
            ParityWithCli(h, engine, adapter, plain);
            Cancellation(h, adapter, plain);
            FailureReporting(h, engine, plain, isolated);
        }
        finally
        {
            Fixtures.Cleanup(workspace);
        }
    }

    private static AnalysisService NewService() =>
        new(new ProcessRunner(), new BackendService(AppContext.BaseDirectory));

    private static void Backend(TestHarness h, string engine, string adapter)
    {
        h.Section("Backend executables");

        h.Test("both bundled executables are present and report the same version", () =>
        {
            var runner = new ProcessRunner();
            var engineVersion = runner.RunAsync(engine, new[] { "--version" }, null, default).GetAwaiter().GetResult();
            var adapterVersion = runner.RunAsync(adapter, new[] { "--version" }, null, default).GetAwaiter().GetResult();

            TestHarness.True(engineVersion.Succeeded, "scc.exe --version should succeed: " + engineVersion.StandardError);
            TestHarness.True(adapterVersion.Succeeded, "codescope-export.exe --version should succeed");
            TestHarness.Equal(engineVersion.StandardOutput.Trim(), adapterVersion.StandardOutput.Trim(),
                "the engine and the adapter must come from the same build");
            TestHarness.Contains(engineVersion.StandardOutput, "scc version", "the version line should be recognisable");
        });

        h.Test("the backend service validates the pair", () =>
        {
            var service = new BackendService(AppContext.BaseDirectory);
            var problem = service.ValidateAsync(default).GetAwaiter().GetResult();
            TestHarness.Null(problem, "validation should pass: " + problem);
            TestHarness.NotNull(service.SccVersion, "the version should be captured");
        });
    }

    private static void BackendFlagSurface(TestHarness h, string adapter)
    {
        h.Section("Backend flag surface");

        h.Test("the adapter accepts every flag the GUI can emit", () =>
        {
            // The GUI builds one argument list for both engines. If it can
            // emit a flag the adapter does not know, the process dies with
            // "flag provided but not defined" — the "analysis failed" report.
            var runner = new ProcessRunner();
            var help = runner.RunAsync(adapter, new[] { "--help" }, null, default).GetAwaiter().GetResult();

            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in (help.StandardOutput + "\n" + help.StandardError).Split('\n'))
            {
                var line = raw.TrimStart();
                if (!line.StartsWith('-'))
                {
                    continue;
                }

                var name = line.TrimStart('-').Split(' ', '=', '\t')[0];
                if (name.Length > 0)
                {
                    known.Add(name);
                }
            }

            TestHarness.True(known.Count > 50, $"the help output should list the flag surface (saw {known.Count})");

            var args = SccArgumentBuilder.BuildNativeArguments(MaximalConfiguration(), @"C:\work");
            var emitted = args
                .Where(a => a.StartsWith("--", StringComparison.Ordinal))
                .Select(a => a.Substring(2).Split('=')[0])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var missing = emitted.Where(f => !known.Contains(f)).ToList();
            TestHarness.True(missing.Count == 0, "the adapter does not know: " + string.Join(", ", missing));

            // The list must actually cover the surface: these are the flags a
            // saved custom preset sent when the run used to fail.
            foreach (var expected in new[]
                     {
                         "count-ignore", "by-file", "uloc", "cognitive",
                         "cocomo-project-type", "locomo", "locomo-preset",
                         "depth", "buckets", "no-fold-authors", "report-skip",
                     })
            {
                TestHarness.True(
                    emitted.Contains(expected, StringComparer.OrdinalIgnoreCase),
                    $"the maximal configuration should emit --{expected}");
            }
        });
    }

    /// <summary>
    /// A configuration with every option off its default, so the argument
    /// builder emits its complete flag surface.
    /// </summary>
    private static ReportConfiguration MaximalConfiguration()
    {
        var config = new ReportConfiguration();
        var a = config.Analysis;

        a.ExcludeDirectories.Add("bin");
        a.ExcludeFiles.Add("generated.txt");
        a.IgnoreFiles.Add("custom.ignore");
        a.NotMatchRegexes.Add("vendor/");
        a.HonourIgnoreFiles = false;
        a.HonourSccIgnore = false;
        a.HonourGitIgnore = false;
        a.HonourGitModules = false;
        a.CountIgnoredFiles = true;
        a.IncludeExtensions.Add("cs");
        a.ExcludeExtensions.Add("json");
        a.SkipLargeFiles = true;
        a.LargeFileLineCount = 10;
        a.LargeFileByteCount = 100;
        a.DetectMinifiedFiles = true;
        a.DetectGeneratedFiles = true;
        a.DetectMinifiedOrGenerated = true;
        a.ExcludeMinifiedFiles = true;
        a.ExcludeGeneratedFiles = true;
        a.ExcludeMinifiedOrGenerated = true;
        a.MinGeneratedLineByteLength = 200;
        a.GeneratedMarkers.Add("custom marker");
        a.DetectBinaryFiles = false;
        a.IncludeSymlinks = true;
        a.CountUnsupportedAsText = true;
        a.RemoveDuplicates = true;
        a.CountAs = "md:HTML";
        a.CountAsPatterns.Add("Dockerfile:dockerfile");
        a.RemapUnknown = "HTML";
        a.RemapAll = "Go";
        a.PerFileOutput = true;
        a.ShowPercentages = true;
        a.WideOutput = true;
        a.CiOutput = true;
        a.Verbose = true;
        a.Debug = true;
        a.Trace = true;
        a.UsePerLanguageCounters = true;
        a.SqlProject = "demo";
        a.ComputeUloc = true;
        a.ComputeDryness = true;
        a.ComputeCognitive = true;
        a.SkipComplexity = true;
        a.SkipCocomo = true;
        a.CocomoProjectType = "embedded";
        a.AverageWage = 200000;
        a.Overhead = 3.0;
        a.EffortAdjustmentFactor = 1.25;
        a.SloccountFormat = true;
        a.SizeUnit = "binary";
        a.SkipSize = true;
        a.CurrencySymbol = "EUR";
        a.EstimateLocomo = true;
        a.CompareCosts = true;
        a.LocomoPreset = "large";
        a.LocomoReviewMinutesPerLine = 0.02;
        a.LocomoInputPrice = 1;
        a.LocomoOutputPrice = 2;
        a.LocomoTokensPerSecond = 50;
        a.LocomoCycles = 10;
        a.LocomoConfig = "1,2,0.5,3,0.3";
        a.HistoryDepth = 50;
        a.TimelineBuckets = 12;
        a.FoldAuthors = false;
        a.FileGcCount = 500;
        a.FileListQueueSize = 2;
        a.FileProcessJobWorkers = 2;
        a.FileListJobWorkers = 2;
        a.FileSummaryJobQueueSize = 2;
        a.DirectoryWalkerJobWorkers = 2;

        // Turning one optional section off also exercises --report-skip.
        config.SetEnabled(ReportSectionId.Cocomo, false);

        return config;
    }

    private static void NativeMode(TestHarness h, string adapter, string project)
    {
        h.Section("In-app analysis (no Git)");

        h.Test("analyses a project whose path contains a space and non-ASCII text", () =>
        {
            var config = new ReportConfiguration();
            var result = NewService().RunAsync(config, project, null, default).GetAwaiter().GetResult();

            TestHarness.True(result.Success, "analysis should succeed: " + result.ErrorMessage);
            TestHarness.NotNull(result.Data, "report data should be present");
            TestHarness.GreaterThan(result.Data!.Totals.Files, 0, "files should be counted");
            TestHarness.GreaterThan(result.Data.Totals.Code, 0, "code lines should be counted");
            TestHarness.GreaterThan(result.Data.Languages.Count, 1, "several languages should be detected");
            TestHarness.False(result.Data.GitAvailable, "a plain folder has no Git history");

            TestHarness.NotNull(result.Data.Uloc, "ULOC data should be present by default");
            TestHarness.NotNull(result.Data.LineLength, "line-length data should be present");
            TestHarness.NotNull(result.Data.Cocomo, "COCOMO should be present by default");
            TestHarness.Equal("organic", result.Data.Cocomo!.ProjectType, "the default project type");
            TestHarness.GreaterThan((long)result.Data.Cocomo.EstimatedCost, 0, "COCOMO cost should be positive");

            var names = result.Data.Languages.Select(l => l.Name).ToList();
            TestHarness.True(names.Contains("Go"), "Go should be detected");
            TestHarness.True(names.Contains("HTML"), "HTML should be detected");
            TestHarness.False(names.Contains("JSON"), "package-lock.json is excluded by the backend default");
        });

        h.Test("honours --count-as style options", () =>
        {
            var config = new ReportConfiguration();
            config.Analysis.CountAs = "md:HTML";
            var result = NewService().RunAsync(config, project, null, default).GetAwaiter().GetResult();

            TestHarness.True(result.Success, "analysis should succeed: " + result.ErrorMessage);
            var names = result.Data!.Languages.Select(l => l.Name).ToList();
            TestHarness.True(names.Contains("HTML"), "the Markdown file should be counted as HTML");
        });

        h.Test("honours include/exclude extension filters", () =>
        {
            var config = new ReportConfiguration();
            config.Analysis.IncludeExtensions.Add("go");
            var result = NewService().RunAsync(config, project, null, default).GetAwaiter().GetResult();

            TestHarness.True(result.Success, "analysis should succeed: " + result.ErrorMessage);
            TestHarness.Equal(1, result.Data!.Languages.Count, "only one language should survive --include-ext go");
            TestHarness.Equal("Go", result.Data.Languages[0].Name, "and it should be Go");
        });

        h.Test("passes a larger average wage straight through to the estimate", () =>
        {
            var cheap = NewService().RunAsync(new ReportConfiguration(), project, null, default).GetAwaiter().GetResult();
            var rich = new ReportConfiguration();
            rich.Analysis.AverageWage = 200000;
            var expensive = NewService().RunAsync(rich, project, null, default).GetAwaiter().GetResult();

            TestHarness.Equal(200000L, expensive.Data!.Cocomo!.AverageWage, "the wage is echoed back");
            TestHarness.True(
                expensive.Data.Cocomo.EstimatedCost > cheap.Data!.Cocomo!.EstimatedCost * 2,
                "a higher wage must produce a materially higher estimate");
        });
    }

    private static void GitSections(TestHarness h, string adapter, string project, string isolated)
    {
        h.Section("In-app analysis (with Git history)");

        h.Test("history sections are produced for a real repository", () =>
        {
            var result = NewService().RunAsync(new ReportConfiguration(), project, null, default).GetAwaiter().GetResult();

            TestHarness.True(result.Success, "analysis should succeed: " + result.ErrorMessage);
            TestHarness.True(result.Data!.GitAvailable, "Git history should be detected");

            TestHarness.NotNull(result.Data.Hotspots, "hotspots data");
            TestHarness.GreaterThan(result.Data.Hotspots!.Records.Count, 0, "hotspot rows");
            TestHarness.NotNull(result.Data.Authors, "authors data");
            TestHarness.GreaterThan(result.Data.Authors!.Rows.Count, 0, "author rows");
            TestHarness.True(result.Data.Authors.Rows.Any(a => a.Name.Contains("Ada")), "the first author should appear");
            TestHarness.True(result.Data.Authors.Rows.Any(a => a.Name.Contains("Grace")), "the second author should appear");
            TestHarness.GreaterThan(result.Data.Authors.BusFactor, 0, "the bus factor should be computed");

            TestHarness.NotNull(result.Data.Coupling, "coupling data");
            TestHarness.GreaterThan(result.Data.Coupling!.Pairs.Count, 0, "coupling rows");

            TestHarness.NotNull(result.Data.Timeline, "timeline data");
            TestHarness.GreaterThan(result.Data.Timeline!.Languages!.Rows.Count, 0, "language timeline rows");
            TestHarness.GreaterThan(result.Data.Timeline.Authors!.Rows.Count, 0, "author timeline rows");

            TestHarness.NotNull(result.Data.Window, "the commit window should be reported");
            TestHarness.True(result.Data.Window!.Commits > 0, "the window should contain commits");

            // The timeline series must line up with the requested bucket count.
            var buckets = result.Data.Timeline.Languages.Rows[0].Deltas.Count;
            TestHarness.Equal(0, buckets % result.Data.Timeline.Languages.Buckets,
                "delta count should be a multiple of the bucket count");
        });

        h.Test("the commit window depth is honoured", () =>
        {
            var config = new ReportConfiguration();
            config.Analysis.HistoryDepth = 2;
            var result = NewService().RunAsync(config, project, null, default).GetAwaiter().GetResult();

            TestHarness.True(result.Success, "analysis should succeed: " + result.ErrorMessage);
            TestHarness.Equal(2, result.Data!.Window!.Commits, "only two commits should be walked");
        });

        h.Test("a non-Git folder reports the history sections as unavailable", () =>
        {
            // Same engine, a folder with no repository above it: the sections
            // must come back empty rather than invented.
            var result = NewService().RunAsync(new ReportConfiguration(), isolated, null, default).GetAwaiter().GetResult();

            TestHarness.True(result.Success, "analysis of a non-repository still succeeds: " + result.ErrorMessage);
            TestHarness.False(result.Data!.GitAvailable, "no Git history is reported");
            TestHarness.Null(result.Data.Hotspots, "no hotspots without a repository");
            TestHarness.Null(result.Data.Authors, "no authors without a repository");
            TestHarness.Null(result.Data.Coupling, "no coupling without a repository");
            TestHarness.Null(result.Data.Timeline, "no timelines without a repository");
            TestHarness.GreaterThan(result.Data.Totals.Files, 0, "the core counts are still produced");
        });
    }

    private static void SectionSelection(TestHarness h, string adapter, string project)
    {
        h.Section("Section selection in the in-app report");

        h.Test("turning a section off removes its data", () =>
        {
            var config = new ReportConfiguration();
            foreach (var toggle in config.Sections)
            {
                toggle.Enabled = false;
            }

            var result = NewService().RunAsync(config, project, null, default).GetAwaiter().GetResult();

            TestHarness.True(result.Success, "analysis should succeed: " + result.ErrorMessage);
            var data = result.Data!;
            TestHarness.Null(data.Uloc, "ULOC should be absent when skipped");
            TestHarness.Null(data.LineLength, "line length should be absent when skipped");
            TestHarness.Null(data.Cocomo, "COCOMO should be absent when skipped");
            TestHarness.Null(data.Locomo, "LOCOMO should be absent when skipped");
            TestHarness.Equal(0, data.Files.Count, "the file table should be empty when skipped");
            TestHarness.GreaterThan(data.Totals.Files, 0, "the core summary is always present");
            TestHarness.GreaterThan(data.Languages.Count, 0, "the language breakdown is always present");
        });

        h.Test("re-enabling individual sections restores exactly those", () =>
        {
            var config = new ReportConfiguration();
            foreach (var toggle in config.Sections)
            {
                toggle.Enabled = false;
            }

            config.SetEnabled(ReportSectionId.UniqueLines, true);

            var result = NewService().RunAsync(config, project, null, default).GetAwaiter().GetResult();

            TestHarness.True(result.Success, "analysis should succeed: " + result.ErrorMessage);
            TestHarness.NotNull(result.Data!.Uloc, "ULOC should be back");
            TestHarness.Null(result.Data.LineLength, "line length should stay off");
            TestHarness.Null(result.Data.Cocomo, "COCOMO should stay off");
        });
    }

    private static void HtmlExport(TestHarness h, string engine, string adapter, string project, string gitProject)
    {
        h.Section("HTML export");

        h.Test("writes a self-contained report and opens no window itself", () =>
        {
            // A repository, so the history sections really are in the output.
            var output = Path.Combine(Path.GetDirectoryName(gitProject)!, "report-out");
            Directory.CreateDirectory(output);

            var config = new ReportConfiguration();
            config.OutputMode = AnalysisOutputMode.Html;
            config.OutputFolder = output;
            config.OutputFileName = "full.html";
            config.ReportTitle = "Fixture Project";

            var result = NewService().RunAsync(config, gitProject, null, default).GetAwaiter().GetResult();

            TestHarness.True(result.Success, "export should succeed: " + result.ErrorMessage);
            TestHarness.NotNull(result.HtmlPath, "the report path should be reported");
            TestHarness.True(File.Exists(result.HtmlPath!), "the report file should exist");

            var html = File.ReadAllText(result.HtmlPath!);
            TestHarness.Contains(html, "Fixture Project", "the title should be applied");
            TestHarness.Contains(html, "<html", "it should be an HTML document");
            TestHarness.False(html.Contains("src=\"http"), "the report should not depend on remote assets");
            TestHarness.Contains(html, "04</span> Hotspots", "the hotspots section should be present");
            TestHarness.Contains(html, "06</span> Authors", "the authors section should be present");
            TestHarness.Contains(html, "COCOMO", "the cost section should be present");
            TestHarness.Contains(html, "09</span> Notable files", "the file table should be present");
        });

        h.Test("--report-skip removes exactly the sections that were turned off", () =>
        {
            var output = Path.Combine(Path.GetDirectoryName(project)!, "report-out");
            Directory.CreateDirectory(output);

            var config = new ReportConfiguration();
            config.OutputMode = AnalysisOutputMode.Html;
            config.OutputFolder = output;
            config.OutputFileName = "trimmed.html";

            foreach (var toggle in config.Sections)
            {
                toggle.Enabled = false;
            }

            config.SetEnabled(ReportSectionId.Cocomo, true);
            config.SetEnabled(ReportSectionId.LanguageBreakdown, true);

            var result = NewService().RunAsync(config, project, null, default).GetAwaiter().GetResult();

            TestHarness.True(result.Success, "export should succeed: " + result.ErrorMessage);
            var html = File.ReadAllText(result.HtmlPath!);

            TestHarness.DoesNotContain(html, "04</span> Hotspots", "hotspots were turned off");
            TestHarness.DoesNotContain(html, "05</span> Change coupling", "coupling was turned off");
            TestHarness.DoesNotContain(html, "03</span> Line length", "the line-length histogram was turned off");
            TestHarness.Contains(html, "COCOMO", "COCOMO was left on");
            TestHarness.Contains(html, "Languages", "the language breakdown is a core section and stays");
        });

        h.Test("an existing report is never replaced without consent", () =>
        {
            var output = Path.Combine(Path.GetDirectoryName(project)!, "report-out");
            var target = Path.Combine(output, "existing.html");
            Directory.CreateDirectory(output);
            File.WriteAllText(target, "<html>original</html>");

            var config = new ReportConfiguration();
            config.OutputMode = AnalysisOutputMode.Html;
            config.OutputFolder = output;
            config.OutputFileName = "existing.html";

            var refused = NewService().RunAsync(config, project, null, default).GetAwaiter().GetResult();
            TestHarness.False(refused.Success, "the export must refuse");
            TestHarness.Contains(refused.ErrorMessage, "already exists", "the refusal must explain itself");
            TestHarness.Equal("<html>original</html>", File.ReadAllText(target), "the original file is untouched");

            config.AllowOverwriteReport = true;
            var allowed = NewService().RunAsync(config, project, null, default).GetAwaiter().GetResult();
            TestHarness.True(allowed.Success, "an explicit confirmation should allow the write: " + allowed.ErrorMessage);
            TestHarness.DoesNotContain(File.ReadAllText(target), "original", "the file should have been replaced");
        });
    }

    private static void NoHtmlInNativeMode(TestHarness h, string adapter, string project)
    {
        h.Section("In-app mode leaves no files behind");

        h.Test("a full in-app analysis writes nothing into the analysed folder", () =>
        {
            var before = Directory.GetFiles(project, "*", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToArray();

            var beforeHtml = before.Count(f => f.EndsWith(".html", StringComparison.OrdinalIgnoreCase));

            var result = NewService().RunAsync(new ReportConfiguration(), project, null, default).GetAwaiter().GetResult();
            TestHarness.True(result.Success, "analysis should succeed: " + result.ErrorMessage);

            var after = Directory.GetFiles(project, "*", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToArray();

            TestHarness.Equal(before.Length, after.Length, "no file may be added to the project");
            TestHarness.True(before.SequenceEqual(after, StringComparer.Ordinal), "the project contents are unchanged");
            TestHarness.Equal(beforeHtml, after.Count(f => f.EndsWith(".html", StringComparison.OrdinalIgnoreCase)),
                "no HTML report may be generated in the in-app mode");
        });

        h.Test("the in-app mode writes no temporary file anywhere it can see", () =>
        {
            var parent = Path.GetDirectoryName(project)!;
            var before = Directory.GetFiles(parent, "*.html", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToArray();

            var config = new ReportConfiguration();
            var result = NewService().RunAsync(config, project, null, default).GetAwaiter().GetResult();
            TestHarness.True(result.Success, "analysis should succeed: " + result.ErrorMessage);

            var after = Directory.GetFiles(parent, "*.html", SearchOption.AllDirectories)
                .Where(f => !before.Contains(f, StringComparer.Ordinal))
                .ToArray();

            TestHarness.Equal(0, after.Length,
                "the in-app mode must not leave an HTML report behind: " + string.Join(", ", after));
        });
    }

    private static void ParityWithCli(TestHarness h, string engine, string adapter, string project)
    {
        h.Section("Parity with the CLI");

        h.Test("the adapter and `scc --format json2` agree on the totals", () =>
        {
            var runner = new ProcessRunner();

            var viaAdapter = runner.RunAsync(
                adapter,
                new[] { "--uloc", project },
                null, default).GetAwaiter().GetResult();

            var viaCli = runner.RunAsync(
                engine,
                new[] { "--format", "json2", "--uloc", project },
                null, default).GetAwaiter().GetResult();

            TestHarness.True(viaAdapter.Succeeded, "the adapter should succeed: " + viaAdapter.StandardError);
            TestHarness.True(viaCli.Succeeded, "the CLI should succeed: " + viaCli.StandardError);

            var adapterJson = JsonDocument.Parse(viaAdapter.StandardOutput);
            var cliJson = JsonDocument.Parse(viaCli.StandardOutput);

            var adapterTotals = adapterJson.RootElement.GetProperty("totals");
            var cliLanguages = cliJson.RootElement.GetProperty("languageSummary");

            long CliSum(string property)
            {
                long sum = 0;
                foreach (var language in cliLanguages.EnumerateArray())
                {
                    sum += language.GetProperty(property).GetInt64();
                }

                return sum;
            }

            TestHarness.Equal(CliSum("Count"), adapterTotals.GetProperty("files").GetInt64(), "file count parity");
            TestHarness.Equal(CliSum("Lines"), adapterTotals.GetProperty("lines").GetInt64(), "total line parity");
            TestHarness.Equal(CliSum("Code"), adapterTotals.GetProperty("code").GetInt64(), "code line parity");
            TestHarness.Equal(CliSum("Comment"), adapterTotals.GetProperty("comment").GetInt64(), "comment parity");
            TestHarness.Equal(CliSum("Blank"), adapterTotals.GetProperty("blank").GetInt64(), "blank parity");
            TestHarness.Equal(CliSum("Complexity"), adapterTotals.GetProperty("complexity").GetInt64(), "complexity parity");
            TestHarness.Equal(CliSum("Bytes"), adapterTotals.GetProperty("bytes").GetInt64(), "byte parity");

            TestHarness.Equal(
                cliJson.RootElement.GetProperty("estimatedCost").GetDouble(),
                adapterJson.RootElement.GetProperty("cocomo").GetProperty("estimatedCost").GetDouble(),
                "COCOMO cost parity");
        });
    }

    private static void Cancellation(TestHarness h, string adapter, string project)
    {
        h.Section("Cancellation");

        h.Test("a cancelled run reports cancellation instead of a result", () =>
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var result = NewService().RunAsync(new ReportConfiguration(), project, null, cts.Token).GetAwaiter().GetResult();

            TestHarness.False(result.Success, "a cancelled run must not report success");
            TestHarness.Contains(result.ErrorMessage, "cancel", "the message should say it was cancelled");
        });
    }

    private static void FailureReporting(TestHarness h, string engine, string project, string isolated)
    {
        h.Section("Failure reporting");

        h.Test("a missing folder is reported clearly, not as a crash", () =>
        {
            var missing = Path.Combine(Path.GetDirectoryName(project)!, "does not exist");
            var result = NewService().RunAsync(new ReportConfiguration(), missing, null, default).GetAwaiter().GetResult();

            TestHarness.False(result.Success, "the run must fail");
            TestHarness.GreaterThan(result.ErrorMessage.Length, 10, "there should be a real explanation");
            TestHarness.Null(result.Data, "no partial data should be handed to the view");
        });

        h.Test("an empty folder analyses to zeroes rather than failing", () =>
        {
            var empty = Path.Combine(Path.GetDirectoryName(project)!, "empty");
            Directory.CreateDirectory(empty);

            var result = NewService().RunAsync(new ReportConfiguration(), empty, null, default).GetAwaiter().GetResult();

            TestHarness.True(result.Success, "an empty folder is not an error: " + result.ErrorMessage);
            TestHarness.Equal(0L, result.Data!.Totals.Files, "no files were counted");
        });

        h.Test("the isolated folder really has no repository above it", () =>
        {
            TestHarness.False(
                Directory.Exists(Path.Combine(isolated, ".git")),
                "the fixture must not contain its own repository");

            var walk = new DirectoryInfo(isolated);
            var found = false;
            while (walk != null)
            {
                if (Directory.Exists(Path.Combine(walk.FullName, ".git")) ||
                    File.Exists(Path.Combine(walk.FullName, ".git")))
                {
                    found = true;
                    break;
                }

                walk = walk.Parent;
            }

            TestHarness.False(found, "no parent directory of the fixture may hold a .git entry");
        });
    }
}