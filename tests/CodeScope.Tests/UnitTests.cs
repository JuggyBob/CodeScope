using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CodeScope.Models;
using CodeScope.Services;
using CodeScope.ViewModels;

namespace CodeScope.Tests;

/// <summary>
/// Tests that need no external process: argument construction, section mapping,
/// persistence and the preset catalogue.
/// </summary>
public static class UnitTests
{
    public static void Run(TestHarness h)
    {
        ArgumentBuilder(h);
        Sections(h);
        Settings(h);
        Presets(h);
        SessionState(h);
        Formatting(h);
        Paths(h);
        History(h);
        Onboarding(h);
    }

    private static void ArgumentBuilder(TestHarness h)
    {
        h.Section("Argument construction");

        h.Test("defaults produce only the flags the backend expects", () =>
        {
            var config = new ReportConfiguration();
            config.Analysis.PerFileOutput = true;
            var args = SccArgumentBuilder.BuildNativeArguments(config, @"C:\work\some project");

            TestHarness.Equal(@"C:\work\some project", args[^1], "the project path must be the last argument");
            TestHarness.True(args.Contains("--by-file"), "--by-file should be on by default");
            TestHarness.True(args.Contains("--uloc"), "--uloc should be on by default");
            TestHarness.False(args.Contains("--no-config"), "the app must never pass --no-config");

            // Defaults that equal the backend's own defaults are omitted so the
            // engine cannot drift away from its documented behaviour.
            TestHarness.False(args.Contains("--avg-wage"), "--avg-wage must not be sent when unchanged");
            TestHarness.False(args.Contains("--overhead"), "--overhead must not be sent when unchanged");
            TestHarness.False(args.Contains("--depth"), "--depth must not be sent when unchanged");
            TestHarness.False(args.Contains("--cocomo-project-type"), "project type must not be sent when unchanged");
        });

        h.Test("negative flags are only sent when the option is off", () =>
        {
            var config = new ReportConfiguration();
            config.Analysis.HonourGitIgnore = false;
            config.Analysis.HonourIgnoreFiles = false;
            config.Analysis.HonourSccIgnore = false;
            config.Analysis.HonourGitModules = false;
            config.Analysis.SkipLargeFiles = true;
            config.Analysis.RemoveDuplicates = true;
            config.Analysis.SkipComplexity = true;

            var args = SccArgumentBuilder.BuildNativeArguments(config, "C:\\x");

            foreach (var flag in new[] { "--no-gitignore", "--no-ignore", "--no-scc-ignore", "--no-gitmodule", "--no-large", "--no-duplicates", "--no-complexity" })
            {
                TestHarness.True(args.Contains(flag), $"{flag} should be present");
            }
        });

        h.Test("numeric options are formatted invariantly", () =>
        {
            var config = new ReportConfiguration();
            config.Analysis.AverageWage = 12345;
            config.Analysis.Overhead = 1.75;
            config.Analysis.EffortAdjustmentFactor = 0.85;
            config.Analysis.HistoryDepth = 250;
            config.Analysis.TimelineBuckets = 24;

            var args = SccArgumentBuilder.BuildNativeArguments(config, "C:\\x");

            TestHarness.True(IndexOfValue(args, "--avg-wage") == "12345", "--avg-wage value");
            TestHarness.True(IndexOfValue(args, "--overhead") == "1.75", "--overhead value must use a dot, not a comma");
            TestHarness.True(IndexOfValue(args, "--eaf") == "0.85", "--eaf value");
            TestHarness.True(IndexOfValue(args, "--depth") == "250", "--depth value");
            TestHarness.True(IndexOfValue(args, "--buckets") == "24", "--buckets value");
        });

        h.Test("list options are comma joined, once per flag", () =>
        {
            var config = new ReportConfiguration();
            config.Analysis.ExcludeDirectories.Add("node_modules");
            config.Analysis.ExcludeDirectories.Add("vendor");
            config.Analysis.ExcludeFiles.Add("*.min.js");

            var args = SccArgumentBuilder.BuildNativeArguments(config, "C:\\x");

            TestHarness.Equal("node_modules,vendor", IndexOfValue(args, "--exclude-dir"), "--exclude-dir value");
            TestHarness.Equal("*.min.js", IndexOfValue(args, "--exclude-file"), "--exclude-file value");
        });

        h.Test("a path with spaces is passed as one argument", () =>
        {
            var config = new ReportConfiguration();
            var path = @"C:\Users\test\my project\ünïcode";
            var args = SccArgumentBuilder.BuildNativeArguments(config, path);
            TestHarness.Equal(path, args[^1], "the path must survive intact");
        });

        h.Test("HTML export asks for a report, a title and the section skip list", () =>
        {
            var config = new ReportConfiguration();
            config.ReportTitle = "My Project";
            config.SetEnabled(ReportSectionId.Cocomo, false);
            config.SetEnabled(ReportSectionId.Files, false);

            var args = SccArgumentBuilder.BuildHtmlArguments(config, "C:\\x", "C:\\out\\report.html");

            TestHarness.True(args.Contains("--report=C:\\out\\report.html"),
                "--report must use the '=' form so the path is its value, got: " + string.Join(" ", args));
            TestHarness.True(args.Contains("--report-title=My Project"), "--report-title");
            TestHarness.True(args.Contains("--report-skip=cocomo,files"), "--report-skip");
            TestHarness.Equal("C:\\x", args[^1], "the project path must be last");
        });

        h.Test("--report-skip is omitted when every optional section is on", () =>
        {
            var config = new ReportConfiguration();
            var args = SccArgumentBuilder.BuildHtmlArguments(config, "C:\\x", "C:\\out\\report.html");
            TestHarness.False(args.Any(a => a.StartsWith("--report-skip", StringComparison.Ordinal)),
                "no skip flag when nothing is disabled");
        });

        h.Test("the in-app argument list also carries the section selection", () =>
        {
            var config = new ReportConfiguration();
            config.SetEnabled(ReportSectionId.Hotspots, false);

            var args = SccArgumentBuilder.BuildNativeArguments(config, "C:\\x");
            TestHarness.True(args.Contains("--report-skip=hotspots"),
                "the in-app report must honour the section toggles too");
        });
    }

    private static void Sections(TestHarness h)
    {
        h.Section("Report section mapping");

        h.Test("the catalogue covers every --report-skip name the backend accepts", () =>
        {
            string[] documented =
            {
                "cocomo", "locomo", "hotspots", "coupling", "authors",
                "timeline", "files", "uloc", "linelength", "card",
            };

            var mapped = ReportSectionInfo.All
                .Where(s => s.SccSkipName != null)
                .Select(s => s.SccSkipName!)
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray();

            TestHarness.Equal(
                string.Join(",", documented.OrderBy(s => s, StringComparer.Ordinal)),
                string.Join(",", mapped),
                "every documented section must map to a toggle");
        });

        h.Test("only the four history sections need Git", () =>
        {
            var needingGit = ReportSectionInfo.All
                .Where(s => s.RequiresGit)
                .Select(s => s.Id)
                .OrderBy(id => id)
                .ToArray();

            TestHarness.Equal(
                string.Join(",", new[]
                {
                    ReportSectionId.Coupling, ReportSectionId.Authors,
                    ReportSectionId.Hotspots, ReportSectionId.Timeline,
                }.OrderBy(id => id)),
                string.Join(",", needingGit),
                "hotspots, coupling, authors and timelines are the Git-dependent sections");
        });

        h.Test("the default selection is every optional section on", () =>
        {
            var defaults = ReportConfiguration.CreateDefaultSections();
            TestHarness.Equal(10, defaults.Count, "ten optional sections");
            TestHarness.False(defaults.Any(t => !t.Enabled), "all optional sections default to on");
        });

        h.Test("turning everything off yields the full skip list", () =>
        {
            var config = new ReportConfiguration();
            foreach (var toggle in config.Sections)
            {
                toggle.Enabled = false;
            }

            var actual = config.BuildReportSkipValue()
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray();

            string[] documented =
            {
                "cocomo", "locomo", "hotspots", "coupling", "authors",
                "timeline", "files", "uloc", "linelength", "card",
            };

            TestHarness.Equal(
                string.Join(",", documented.OrderBy(s => s, StringComparer.Ordinal)),
                string.Join(",", actual),
                "--report-skip with every optional section disabled");
        });
    }

    private static void Settings(TestHarness h)
    {
        h.Section("Settings and presets");

        h.Test("settings round-trip through the store", () =>
        {
            var root = Fixtures.CreateWorkspace();
            try
            {
                var store = new SettingsStore(Path.Combine(root, "appdata"));
                var settings = store.LoadSettings();
                TestHarness.Equal(AppTheme.Dark, settings.Theme, "first run defaults to dark");

                settings.Theme = AppTheme.System;
                settings.LastProjectFolder = @"C:\work";
                settings.LastPresetName = "Overview";
                settings.CustomPresets.Add("My Preset");
                store.SaveSettings(settings);

                var reloaded = new SettingsStore(Path.Combine(root, "appdata")).LoadSettings();
                TestHarness.Equal(AppTheme.System, reloaded.Theme, "theme survives a reload");
                TestHarness.Equal(@"C:\work", reloaded.LastProjectFolder, "last folder survives a reload");
                TestHarness.Equal("Overview", reloaded.LastPresetName, "last preset survives a reload");
                TestHarness.Equal(1, reloaded.CustomPresets.Count, "custom preset list survives a reload");
            }
            finally
            {
                Fixtures.Cleanup(root);
            }
        });

        h.Test("a corrupt settings file does not throw", () =>
        {
            var root = Fixtures.CreateWorkspace();
            try
            {
                var store = new SettingsStore(Path.Combine(root, "appdata"));
                store.EnsureDirectories();
                File.WriteAllText(store.SettingsFilePath, "{ this is not json ");

                var settings = store.LoadSettings();
                TestHarness.Equal(AppTheme.Dark, settings.Theme, "a broken file falls back to defaults");
            }
            finally
            {
                Fixtures.Cleanup(root);
            }
        });

        h.Test("presets round-trip and can be listed", () =>
        {
            var root = Fixtures.CreateWorkspace();
            try
            {
                var store = new SettingsStore(Path.Combine(root, "appdata"));
                var config = new ReportConfiguration();
                config.Analysis.AverageWage = 99999;
                config.Analysis.ExcludeDirectories.Add("dist");
                config.SetEnabled(ReportSectionId.ShareCard, false);
                config.OutputMode = AnalysisOutputMode.Html;

                store.SavePreset("Team defaults", config);

                TestHarness.True(store.LoadPreset("Team defaults", out var loaded), "the preset should load");
                TestHarness.Equal(99999L, loaded.Analysis.AverageWage, "options survive the round trip");
                TestHarness.Equal(1, loaded.Analysis.ExcludeDirectories.Count, "lists survive the round trip");
                TestHarness.Equal("dist", loaded.Analysis.ExcludeDirectories[0], "list contents survive");
                TestHarness.False(loaded.IsEnabled(ReportSectionId.ShareCard), "section state survives");
                TestHarness.Equal(AnalysisOutputMode.Html, loaded.OutputMode, "output mode survives");

                var names = store.ListPresets();
                TestHarness.True(names.Contains("Team defaults"), "the preset is listed");

                store.DeletePreset("Team defaults");
                TestHarness.False(store.LoadPreset("Team defaults", out _), "a deleted preset is gone");
            }
            finally
            {
                Fixtures.Cleanup(root);
            }
        });

        h.Test("a preset name with path separators is sanitised", () =>
        {
            var root = Fixtures.CreateWorkspace();
            try
            {
                var store = new SettingsStore(Path.Combine(root, "appdata"));
                store.SavePreset("../escape", new ReportConfiguration());
                TestHarness.True(store.LoadPreset("../escape", out _), "the sanitised name still loads");
                TestHarness.False(File.Exists(Path.Combine(root, "escape.json")), "nothing escapes the preset folder");
            }
            finally
            {
                Fixtures.Cleanup(root);
            }
        });
    }

    private static void Presets(TestHarness h)
    {
        h.Section("Built-in presets");

        h.Test("all built-in presets exist and are distinguishable", () =>
        {
            foreach (var name in PresetCatalog.BuiltinNames)
            {
                TestHarness.True(PresetCatalog.TryGetBuiltin(name, out var config), $"{name} should resolve");
                TestHarness.NotNull(config, $"{name} should produce a configuration");
                TestHarness.Equal(
                    ReportSectionInfo.All.Count(t => !t.IsCore),
                    config.Sections.Count,
                    $"{name} should carry every optional section");
            }

            TestHarness.False(
                PresetCatalog.TryGetBuiltin("Not A Preset", out _),
                "an unknown preset name must not resolve");

            // The Overview preset is the small one; Full Report is the large one.
            PresetCatalog.TryGetBuiltin(PresetCatalog.OverviewName, out var overview);
            PresetCatalog.TryGetBuiltin(PresetCatalog.FullReportName, out var full);

            var overviewOn = overview.Sections.Count(t => t.Enabled);
            var fullOn = full.Sections.Count(t => t.Enabled);
            TestHarness.True(overviewOn < fullOn, "Overview must enable fewer sections than Full Report");
            TestHarness.True(fullOn == 10, "Full Report enables every optional section");
        });

        h.Test("Git Insights leaves per-file output off", () =>
        {
            PresetCatalog.TryGetBuiltin(PresetCatalog.GitInsightsName, out var config);
            TestHarness.False(config.Analysis.PerFileOutput, "Git Insights does not need per-file rows");
            foreach (var id in new[]
                     {
                         ReportSectionId.Hotspots, ReportSectionId.Coupling,
                         ReportSectionId.Authors, ReportSectionId.Timeline,
                     })
            {
                TestHarness.True(config.IsEnabled(id), $"{id} should be on in Git Insights");
            }
        });

        h.Test("applying a preset does not alias the original", () =>
        {
            PresetCatalog.TryGetBuiltin(PresetCatalog.CodeQualityName, out var original);
            var copy = original.Clone();
            copy.Analysis.AverageWage = 1;
            TestHarness.NotEqual(original.Analysis.AverageWage, 1, "clones must be independent");
        });

        h.Test("a preset saved before the coupling options were retired still loads", () =>
        {
            var root = Fixtures.CreateWorkspace();
            try
            {
                var store = new SettingsStore(Path.Combine(root, "appdata"));
                store.SavePreset("Legacy", new ReportConfiguration());

                // An older build wrote sortBy / weightCouplingByComplexity /
                // couplingForFile into the preset. They must be ignored on
                // load, never turned into flags the backend rejects — that
                // mismatch is what made a saved preset report "analysis failed".
                var path = Path.Combine(store.PresetsDirectory, "Legacy.json");
                var json = File.ReadAllText(path);
                TestHarness.Contains(json, "\"analysis\": {", "the preset schema keeps its camelCase analysis object");
                json = json.Replace(
                    "\"analysis\": {",
                    "\"analysis\": {\n    \"sortBy\": \"cognitive\",\n    \"weightCouplingByComplexity\": true,\n    \"couplingForFile\": \"src/core.cs\",");
                File.WriteAllText(path, json);

                TestHarness.True(store.LoadPreset("Legacy", out var preset), "the legacy preset must still load");

                var line = string.Join(' ', SccArgumentBuilder.BuildNativeArguments(preset, @"C:\work"));
                TestHarness.DoesNotContain(line, "--sort", "the retired --sort must not be forwarded");
                TestHarness.DoesNotContain(line, "--coupling-weighted", "the retired --coupling-weighted must not be forwarded");
                TestHarness.DoesNotContain(line, "--coupling-for", "the retired --coupling-for must not be forwarded");
            }
            finally
            {
                Fixtures.Cleanup(root);
            }
        });
    }

    private static void SessionState(TestHarness h)
    {
        h.Section("Session state");

        h.Test("re-selecting the current preset does not discard the session", () =>
        {
            var root = Fixtures.CreateWorkspace();
            try
            {
                var store = new SettingsStore(Path.Combine(root, "appdata"));
                var viewModel = new MainViewModel(
                    store,
                    new Services.BackendService(AppContext.BaseDirectory),
                    new Services.AnalysisService(new Services.ProcessRunner(), new Services.BackendService(AppContext.BaseDirectory)),
                    new Services.ThemeManager())
                {
                    ProjectPath = root,
                };

                viewModel.InitializeAsync().GetAwaiter().GetResult();

                // What the user picks after startup.
                viewModel.ApplyPreset(PresetCatalog.OverviewName);
                viewModel.Configuration.OutputFolder = Path.Combine(root, "reports");
                viewModel.Configuration.ReportTitle = "Nightly";

                // The preset list raises a selection change when it first
                // renders; that echo must not undo what the user just set.
                viewModel.ApplyPreset(PresetCatalog.OverviewName);

                TestHarness.Equal(PresetCatalog.OverviewName, viewModel.SelectedPreset, "the preset stays selected");
                TestHarness.Equal(
                    Path.Combine(root, "reports"),
                    viewModel.Configuration.OutputFolder,
                    "the chosen output folder survives a preset re-selection");
                TestHarness.Equal("Nightly", viewModel.Configuration.ReportTitle, "the title survives too");
                TestHarness.False(
                    viewModel.Configuration.IsEnabled(ReportSectionId.Cocomo),
                    "the preset's own section selection is still in force");
            }
            finally
            {
                Fixtures.Cleanup(root);
            }
        });

        h.Test("picking a different preset does apply it", () =>
        {
            var root = Fixtures.CreateWorkspace();
            try
            {
                var store = new SettingsStore(Path.Combine(root, "appdata"));
                var backend = new Services.BackendService(AppContext.BaseDirectory);
                var viewModel = new MainViewModel(
                    store,
                    backend,
                    new Services.AnalysisService(new Services.ProcessRunner(), backend),
                    new Services.ThemeManager())
                {
                    ProjectPath = root,
                };

                viewModel.InitializeAsync().GetAwaiter().GetResult();

                viewModel.ApplyPreset(PresetCatalog.OverviewName);
                TestHarness.False(viewModel.Configuration.IsEnabled(ReportSectionId.Hotspots), "Overview drops hotspots");

                viewModel.ApplyPreset(PresetCatalog.GitInsightsName);
                TestHarness.True(viewModel.Configuration.IsEnabled(ReportSectionId.Hotspots), "Git Insights keeps it");
                TestHarness.Equal(PresetCatalog.GitInsightsName, viewModel.SelectedPreset, "the selection follows");
            }
            finally
            {
                Fixtures.Cleanup(root);
            }
        });

        h.Test("the typed report name is normalised and remembered", () =>
        {
            var root = Fixtures.CreateWorkspace();
            try
            {
                var store = new SettingsStore(Path.Combine(root, "appdata"));
                var backend = new Services.BackendService(AppContext.BaseDirectory);
                var viewModel = new MainViewModel(
                    store,
                    backend,
                    new Services.AnalysisService(new Services.ProcessRunner(), backend),
                    new Services.ThemeManager());

                viewModel.InitializeAsync().GetAwaiter().GetResult();

                viewModel.OutputFileNameInput = "nightly-report";
                viewModel.SaveReportFileNameCommand.Execute(null);

                TestHarness.Equal("nightly-report.html", viewModel.OutputFileNameInput,
                    "an extensionless name gains .html");
                TestHarness.Equal("Saved as nightly-report.html", viewModel.OutputFileConfirmation,
                    "the confirmation is shown inline");
                TestHarness.Equal("nightly-report.html", store.LoadSettings().LastReportFileName,
                    "the name is persisted for the next launch");

                viewModel.OutputFileNameInput = "audit.htm";
                viewModel.SaveReportFileNameCommand.Execute(null);
                TestHarness.Equal("audit.htm", viewModel.OutputFileNameInput,
                    "an explicit extension is kept");

                viewModel.OutputFileNameInput = "   ";
                viewModel.SaveReportFileNameCommand.Execute(null);
                TestHarness.Equal(string.Empty, viewModel.OutputFileConfirmation,
                    "an empty name shows no confirmation");
                TestHarness.True(viewModel.ErrorMessage.Length > 0,
                    "an empty name is explained");
            }
            finally
            {
                Fixtures.Cleanup(root);
            }
        });
    }

    private static void Formatting(TestHarness h)
    {

        h.Test("bytes render with a sensible unit", () =>
        {
            TestHarness.Equal("0 B", Format.Bytes(0, "si"), "zero bytes");
            TestHarness.Equal("1.5 kB", Format.Bytes(1500, "si"), "SI kilobytes");
            TestHarness.Equal("1.46 KiB", Format.Bytes(1500, "binary"), "binary kibibytes");
            TestHarness.Equal("$1,234", Format.Currency(1234.4, "$"), "currency rounding");
        });

        h.Test("dates and durations render for humans", () =>
        {
            var parsed = DateTimeOffset.Parse("2026-10-18T14:42:00Z");
            TestHarness.Equal(parsed.LocalDateTime.ToString("yyyy-MM-dd HH:mm"), Format.DateTime("2026-10-18T14:42:00Z"),
                "an ISO timestamp renders in local time");
            TestHarness.Equal(string.Empty, Format.DateTime("not a date"), "garbage in, empty out");
            TestHarness.Equal(string.Empty, Format.DateTime(null), "no timestamp, no text");
            TestHarness.Equal("30 min", Format.Duration(0.5), "sub-hour durations");
            TestHarness.Equal("950 hr", Format.Duration(950), "long whole-hour durations");
        });
    }

    private static void Paths(TestHarness h)
    {
        h.Test("engine paths become native Windows paths for Explorer", () =>
        {
            var native = BackendService.ToNativePath("A:/projects/repo/src/Main.kt");
            TestHarness.False(native.Contains('/'), "forward slashes must be replaced");
            TestHarness.True(native.EndsWith(Path.Combine("src", "Main.kt")), "the path must keep its shape");
            TestHarness.Equal(Path.GetFullPath(native), native, "the result must already be absolute");

            var relative = BackendService.ToNativePath("src/Main.kt");
            TestHarness.True(Path.IsPathRooted(relative), "relative engine paths must be made absolute");
            TestHarness.False(relative.Contains('/'), "relative paths must be normalised too");
        });
    }

    private static void History(TestHarness h)
    {
        h.Section("Analysis history");

        h.Test("a finished run is stored and can be reopened", () =>
        {
            var root = Fixtures.CreateWorkspace();
            try
            {
                var store = new HistoryStore(Path.Combine(root, "appdata"));
                var entry = new HistoryEntry
                {
                    Id = HistoryStore.NewId(DateTimeOffset.UtcNow),
                    Timestamp = DateTimeOffset.UtcNow,
                    ProjectPath = @"C:\work\demo",
                    ProjectName = "demo",
                    Mode = AnalysisOutputMode.InApp,
                    PresetName = "Full Report",
                    Files = 12,
                    Lines = 345,
                    Code = 210,
                    Complexity = 17,
                    DurationMs = 420,
                    Data = new SccReportData
                    {
                        RepoName = "demo",
                        Path = @"C:\work\demo",
                        Totals = new Totals { Files = 12, Lines = 345, Code = 210 },
                    },
                };

                store.Save(entry);
                var stored = store.Load();

                TestHarness.Equal(1, stored.Count, "the run must be listed");
                TestHarness.Equal("demo", stored[0].ProjectName, "the folder name must be kept");
                TestHarness.Equal(12, stored[0].Files, "the file count must be kept");
                TestHarness.NotNull(stored[0].Data, "the report data must be kept so the run can be reopened");
                TestHarness.Equal(345, stored[0].Data!.Totals.Lines, "the stored totals must survive the round trip");
            }
            finally
            {
                Fixtures.Cleanup(root);
            }
        });

        h.Test("the newest run is listed first and the log stays bounded", () =>
        {
            var root = Fixtures.CreateWorkspace();
            try
            {
                var store = new HistoryStore(Path.Combine(root, "appdata"));

                for (var i = 0; i < HistoryStore.MaxEntries + 5; i++)
                {
                    store.Save(new HistoryEntry
                    {
                        Id = HistoryStore.NewId(DateTimeOffset.UtcNow),
                        Timestamp = DateTimeOffset.UtcNow.AddSeconds(i),
                        ProjectName = "run" + i,
                        ProjectPath = @"C:\work\run" + i,
                    });
                }

                var stored = store.Load();
                TestHarness.Equal(HistoryStore.MaxEntries, stored.Count, "the log must be capped");
                TestHarness.Equal(
                    "run" + (HistoryStore.MaxEntries + 4),
                    stored[0].ProjectName,
                    "the newest run must be listed first");
            }
            finally
            {
                Fixtures.Cleanup(root);
            }
        });

        h.Test("a run can be removed and the whole log cleared", () =>
        {
            var root = Fixtures.CreateWorkspace();
            try
            {
                var store = new HistoryStore(Path.Combine(root, "appdata"));
                var first = new HistoryEntry
                {
                    Id = HistoryStore.NewId(DateTimeOffset.UtcNow),
                    Timestamp = DateTimeOffset.UtcNow,
                    ProjectName = "one",
                };
                var second = new HistoryEntry
                {
                    Id = HistoryStore.NewId(DateTimeOffset.UtcNow),
                    Timestamp = DateTimeOffset.UtcNow.AddMinutes(1),
                    ProjectName = "two",
                };

                store.Save(first);
                store.Save(second);
                TestHarness.Equal(2, store.Load().Count, "both runs must be listed");

                TestHarness.True(store.Delete(first.Id), "the run must be removed");
                TestHarness.Equal(1, store.Load().Count, "the other run must survive");

                store.Clear();
                TestHarness.Equal(0, store.Load().Count, "clearing must empty the log");
            }
            finally
            {
                Fixtures.Cleanup(root);
            }
        });

        h.Test("a damaged history file never hides the rest of the log", () =>
        {
            var root = Fixtures.CreateWorkspace();
            try
            {
                var store = new HistoryStore(Path.Combine(root, "appdata"));
                store.Save(new HistoryEntry
                {
                    Id = "good",
                    Timestamp = DateTimeOffset.UtcNow,
                    ProjectName = "good",
                });

                Directory.CreateDirectory(store.HistoryDirectory);
                File.WriteAllText(Path.Combine(store.HistoryDirectory, "broken.json"), "{ this is not json");

                var stored = store.Load();
                TestHarness.Equal(1, stored.Count, "the readable run must still be listed");
            }
            finally
            {
                Fixtures.Cleanup(root);
            }
        });

        h.Test("a failed run shows the error banner", () =>
        {
            var root = Fixtures.CreateWorkspace();
            try
            {
                var store = new SettingsStore(Path.Combine(root, "appdata"));
                var viewModel = new MainViewModel(
                    store,
                    new Services.BackendService(AppContext.BaseDirectory),
                    new Services.AnalysisService(new Services.ProcessRunner(), new Services.BackendService(AppContext.BaseDirectory)),
                    new Services.ThemeManager())
                {
                    ProjectPath = Path.Combine(root, "does not exist"),
                };

                viewModel.InitializeAsync().GetAwaiter().GetResult();

                var raised = new List<string>();
                viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName ?? string.Empty);

                TestHarness.False(viewModel.HasError, "there is nothing to report yet");

                viewModel.GenerateCommand.Execute(null);

                var deadline = DateTime.UtcNow.AddSeconds(120);
                while (viewModel.IsRunning && DateTime.UtcNow < deadline)
                {
                    Thread.Sleep(100);
                }

                TestHarness.True(viewModel.HasError, "the banner must appear when the run fails");
                TestHarness.True(viewModel.ErrorMessage.Length > 0, "the banner must carry the reason");
                TestHarness.True(raised.Contains("HasError"), "the banner must be told to appear");
            }
            finally
            {
                Fixtures.Cleanup(root);
            }
        });

        h.Test("a run keeps the sections it was produced with", () =>
        {
            var root = Fixtures.CreateWorkspace();
            try
            {
                var store = new HistoryStore(Path.Combine(root, "appdata"));
                var entry = new HistoryEntry
                {
                    Id = HistoryStore.NewId(DateTimeOffset.UtcNow),
                    Timestamp = DateTimeOffset.UtcNow,
                    ProjectName = "demo",
                    DisabledSections = { "Hotspots", "Coupling" },
                };

                store.Save(entry);
                var stored = store.Load();

                TestHarness.Equal(1, stored.Count, "the run must be listed");
                TestHarness.Equal(2, stored[0].DisabledSections.Count, "the section selection must be kept");
                TestHarness.True(
                    stored[0].DisabledSections.Contains("Hotspots"),
                    "the section that was off must still be off");
            }
            finally
            {
                Fixtures.Cleanup(root);
            }
        });

        h.Test("a history row announces the project, not the type name", () =>        {
            var row = new HistoryEntryViewModel(new HistoryEntry
            {
                Id = "x",
                ProjectName = "DiscountHunterAndroid",
                Timestamp = DateTimeOffset.Now,
            });

            TestHarness.Equal("DiscountHunterAndroid", row.ToString(), "the row must be announced by folder name");
            TestHarness.True(row.MetricsText.Contains("files"), "the row must show what was analysed");
            TestHarness.True(row.WhenText.Length > 0, "the row must say when it ran");
        });

        h.Test("a table remembers its sort and flips the direction", () =>
        {
            var state = new TableSortState("Files", "Lines", "Lines", descending: true);
            TestHarness.Equal("Lines", state.Heading, "the sorted heading must be remembered");
            TestHarness.Equal("Lines", state.Key, "the sort key must be remembered");
            TestHarness.True(state.Descending, "the direction must be remembered");

            state.Set("Lines", "Lines", descending: false);
            TestHarness.False(state.Descending, "the same column must flip the direction");

            state.Set("Code", "Code", descending: false);
            TestHarness.Equal("Code", state.Heading, "a new column must take over");
            TestHarness.False(state.Descending, "a new column must start ascending");
        });

        h.Test("the report orders a table by the column the user picked", () =>
        {
            var data = new SccReportData
            {
                Languages =
                {
                    new LanguageRow { Name = "A", Lines = 10, Bytes = 1 },
                    new LanguageRow { Name = "B", Lines = 30, Bytes = 2 },
                    new LanguageRow { Name = "C", Lines = 20, Bytes = 3 },
                },
            };

            var viewModel = new ReportViewModel(
                new ReportConfiguration(),
                data,
                () => { },
                () => Task.CompletedTask);

            // The table arrives sorted by size, from the largest down.
            TestHarness.Equal("B", viewModel.Languages[0].Name, "the language table must arrive sorted by lines");
            TestHarness.True(viewModel.SortState("Languages")!.Descending, "the first order must be descending");

            // A click on a heading sorts by it, and the heading is remembered
            // so the arrow knows which column to point at.
            viewModel.SetSortState("Languages", "Name", "Name", descending: false);
            TestHarness.Equal("A", viewModel.Languages[0].Name, "the table must be ordered by the picked column");
            TestHarness.Equal("Name", viewModel.SortState("Languages")!.Heading, "the heading must be remembered");

            // The same heading again flips the direction.
            viewModel.SetSortState("Languages", "Name", "Name", descending: true);
            TestHarness.Equal("C", viewModel.Languages[0].Name, "the direction must be flipped");
        });

        h.Test("clicking the same heading turns the table around", () =>
        {
            var data = new SccReportData
            {
                Languages =
                {
                    new LanguageRow { Name = "A", Lines = 10 },
                    new LanguageRow { Name = "B", Lines = 30 },
                    new LanguageRow { Name = "C", Lines = 20 },
                },
            };

            var viewModel = new ReportViewModel(
                new ReportConfiguration(),
                data,
                () => { },
                () => Task.CompletedTask);

            // A new heading starts with the smallest value on top.
            viewModel.ToggleSort("Languages", "Name", "Name");
            TestHarness.Equal("A", viewModel.Languages[0].Name, "a new heading must start ascending");
            TestHarness.False(viewModel.SortState("Languages")!.Descending, "a new heading must start ascending");

            // The same heading again turns the table around.
            viewModel.ToggleSort("Languages", "Name", "Name");
            TestHarness.Equal("C", viewModel.Languages[0].Name, "the same heading must turn the table around");
            TestHarness.True(viewModel.SortState("Languages")!.Descending, "the direction must be flipped");

            // And once more, back the way it was.
            viewModel.ToggleSort("Languages", "Name", "Name");
            TestHarness.Equal("A", viewModel.Languages[0].Name, "the direction must flip back");
            TestHarness.False(viewModel.SortState("Languages")!.Descending, "the direction must flip back");
        });

        h.Test("a heading that appears in two tables keeps its own arrow", () =>
        {
            var data = new SccReportData
            {
                Files =
                {
                    new FileRow { Filename = "b.cs", Location = "src/b.cs" },
                    new FileRow { Filename = "a.cs", Location = "src/a.cs" },
                },
                Hotspots = new HotspotsData
                {
                    Records =
                    {
                        new HotspotRow { File = "src/b.cs", Complexity = 1 },
                        new HotspotRow { File = "src/a.cs", Complexity = 2 },
                    },
                },
            };

            var viewModel = new ReportViewModel(
                new ReportConfiguration(),
                data,
                () => { },
                () => Task.CompletedTask);

            // Both tables have a "File" heading, but each keeps its own sort.
            viewModel.SetSortState("Files", "File", "Location", descending: false);
            viewModel.SetSortState("Hotspots", "File", "File", descending: false);

            TestHarness.Equal("File", viewModel.SortState("Files")!.Heading, "the file table keeps its heading");
            TestHarness.Equal("File", viewModel.SortState("Hotspots")!.Heading, "the hotspot table keeps its heading");
            TestHarness.Equal("src/a.cs", viewModel.Files[0].Location, "the file table must be ordered by the path it shows");
            TestHarness.Equal("src/a.cs", viewModel.Hotspots[0].File, "the hotspot table must be ordered by its own column");
        });

        h.Test("a damaged history file does not hide the rest", () =>
        {
            var root = Fixtures.CreateWorkspace();
            try
            {
                var store = new HistoryStore(Path.Combine(root, "appdata"));
                var good = new HistoryEntry
                {
                    Id = HistoryStore.NewId(DateTimeOffset.UtcNow),
                    Timestamp = DateTimeOffset.UtcNow,
                    ProjectName = "demo",
                };

                store.Save(good);

                // A file the app wrote badly, and one that was tampered with.
                File.WriteAllText(Path.Combine(store.HistoryDirectory, "broken.json"), "{ not json at all");
                File.WriteAllText(Path.Combine(store.HistoryDirectory, "nullid.json"), "{\"id\":null}");

                var stored = store.Load();
                TestHarness.Equal(1, stored.Count, "the good run must still be there");
                TestHarness.Equal("demo", stored[0].ProjectName, "the good run must be the one that is listed");
            }
            finally
            {
                Fixtures.Cleanup(root);
            }
        });

        h.Test("a history identifier cannot reach outside the history folder", () =>
        {
            var root = Fixtures.CreateWorkspace();
            try
            {
                var store = new HistoryStore(Path.Combine(root, "appdata"));
                var outside = Path.Combine(root, "precious.txt");
                File.WriteAllText(outside, "keep me");

                TestHarness.False(store.Delete("..\\precious"), "a traversing identifier must be refused");
                TestHarness.True(File.Exists(outside), "the file outside the folder must be untouched");
            }
            finally
            {
                Fixtures.Cleanup(root);
            }
        });
    }

    private static void Onboarding(TestHarness h)
    {
        h.Section("First-run walkthrough");

        h.Test("the walkthrough flag and the sidebar state are remembered", () =>
        {
            var root = Fixtures.CreateWorkspace();
            try
            {
                var store = new SettingsStore(Path.Combine(root, "appdata"));
                var settings = store.LoadSettings();
                TestHarness.False(settings.OnboardingCompleted, "a fresh install must show the walkthrough");
                TestHarness.True(settings.SidebarExpanded, "the sidebar starts open");

                settings.OnboardingCompleted = true;
                settings.SidebarExpanded = false;
                store.SaveSettings(settings);

                var reloaded = store.LoadSettings();
                TestHarness.True(reloaded.OnboardingCompleted, "the finished walkthrough must be remembered");
                TestHarness.False(reloaded.SidebarExpanded, "the collapsed sidebar must be remembered");
            }
            finally
            {
                Fixtures.Cleanup(root);
            }
        });
    }

    private static string IndexOfValue(IReadOnlyList<string> args, string flag)
    {
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (args[i] == flag)
            {
                return args[i + 1];
            }
        }

        return "<flag not present>";
    }
}