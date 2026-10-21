using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CodeScope.Models;

namespace CodeScope.Services;

/// <summary>The phase of a run, for the status line.</summary>
public enum AnalysisStage
{
    Preparing,
    Analysing,
    Exporting,
    Rendering,
    Done,
    Failed,
}

/// <summary>A single user-visible log line from the backend.</summary>
public sealed class DiagnosticEntry
{
    public DiagnosticSeverity Severity { get; init; } = DiagnosticSeverity.Information;

    public string Message { get; init; } = string.Empty;
}

public enum DiagnosticSeverity
{
    Information,
    Warning,
    Error,
}

/// <summary>The outcome of a full analysis run, in either output mode.</summary>
public sealed class AnalysisResult
{
    public bool Success { get; init; }

    public AnalysisStage Stage { get; init; }

    public SccReportData? Data { get; init; }

    public AnalysisOutputMode Mode { get; init; }

    public string? HtmlPath { get; init; }

    public string ErrorMessage { get; init; } = string.Empty;

    public IReadOnlyList<DiagnosticEntry> Diagnostics { get; init; } = Array.Empty<DiagnosticEntry>();

    public long ElapsedMilliseconds { get; init; }

    public string RepoName => Data?.RepoName ?? string.Empty;

    public string ProjectPath { get; init; } = string.Empty;
}

/// <summary>
/// Runs the frozen backend for the configured project. The same
/// <see cref="ReportConfiguration"/> drives both output modes so the numbers and
/// the section selection can never drift apart.
/// </summary>
public sealed class AnalysisService
{
    private readonly IProcessRunner _runner;
    private readonly BackendService _backend;

    public AnalysisService(IProcessRunner runner, BackendService backend)
    {
        _runner = runner;
        _backend = backend;
    }

    public async Task<AnalysisResult> RunAsync(
        ReportConfiguration configuration,
        string projectPath,
        IProgress<AnalysisStage>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(AnalysisStage.Preparing);

        var diagnostics = new List<DiagnosticEntry>();
        var stopwatch = Stopwatch.StartNew();

        if (configuration.OutputMode == AnalysisOutputMode.Html)
        {
            return await RunHtmlExportAsync(configuration, projectPath, progress, cancellationToken, diagnostics, stopwatch);
        }

        return await RunNativeAsync(configuration, projectPath, progress, cancellationToken, diagnostics, stopwatch);
    }

    private async Task<AnalysisResult> RunNativeAsync(
        ReportConfiguration configuration,
        string projectPath,
        IProgress<AnalysisStage>? progress,
        CancellationToken cancellationToken,
        List<DiagnosticEntry> diagnostics,
        Stopwatch stopwatch)
    {
        var arguments = SccArgumentBuilder.BuildNativeArguments(configuration, projectPath);

        progress?.Report(AnalysisStage.Analysing);
        var result = await _runner.RunAsync(_backend.ExportToolPath, arguments, null, cancellationToken);

        if (result.WasCancelled)
        {
            return new AnalysisResult
            {
                Success = false,
                Stage = AnalysisStage.Failed,
                Mode = configuration.OutputMode,
                ErrorMessage = "The analysis was cancelled.",
                Diagnostics = diagnostics,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                ProjectPath = projectPath,
            };
        }

        if (!result.Succeeded)
        {
            diagnostics.Add(new DiagnosticEntry
            {
                Severity = DiagnosticSeverity.Error,
                Message = result.StandardError.Trim(),
            });

            return new AnalysisResult
            {
                Success = false,
                Stage = AnalysisStage.Failed,
                Mode = configuration.OutputMode,
                ErrorMessage = BuildEngineErrorMessage(result.ExitCode, result.StandardError),
                Diagnostics = diagnostics,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                ProjectPath = projectPath,
            };
        }

        if (result.StandardError.Length > 0)
        {
            foreach (var line in SplitLines(result.StandardError))
            {
                if (line.Length > 0)
                {
                    diagnostics.Add(new DiagnosticEntry { Severity = DiagnosticSeverity.Warning, Message = line });
                }
            }
        }

        progress?.Report(AnalysisStage.Rendering);

        SccReportData data;
        try
        {
            data = ParseJson(result.StandardOutput);
        }
        catch (JsonException ex)
        {
            return new AnalysisResult
            {
                Success = false,
                Stage = AnalysisStage.Failed,
                Mode = configuration.OutputMode,
                ErrorMessage = "The backend returned data the application could not read: " + ex.Message,
                Diagnostics = diagnostics,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                ProjectPath = projectPath,
            };
        }

        return new AnalysisResult
        {
            Success = true,
            Stage = AnalysisStage.Done,
            Mode = configuration.OutputMode,
            Data = data,
            Diagnostics = diagnostics,
            ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
            ProjectPath = projectPath,
        };
    }

    private async Task<AnalysisResult> RunHtmlExportAsync(
        ReportConfiguration configuration,
        string projectPath,
        IProgress<AnalysisStage>? progress,
        CancellationToken cancellationToken,
        List<DiagnosticEntry> diagnostics,
        Stopwatch stopwatch)
    {
        var reportPath = Path.Combine(
            string.IsNullOrEmpty(configuration.OutputFolder) ? projectPath : configuration.OutputFolder,
            string.IsNullOrEmpty(configuration.OutputFileName) ? "scc-report.html" : configuration.OutputFileName);

        // scc's own `--report=<path>` overwrites without asking, so the guard
        // lives here: nothing is replaced unless the user said so.
        if (File.Exists(reportPath) && !configuration.AllowOverwriteReport)
        {
            return new AnalysisResult
            {
                Success = false,
                Stage = AnalysisStage.Failed,
                Mode = configuration.OutputMode,
                ErrorMessage =
                    $"A report already exists at:\n{reportPath}\n\n" +
                    "Confirm replacing it, or choose a different file name.",
                Diagnostics = diagnostics,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                ProjectPath = projectPath,
            };
        }

        var arguments = SccArgumentBuilder.BuildHtmlArguments(configuration, projectPath, reportPath);

        progress?.Report(AnalysisStage.Exporting);
        var result = await _runner.RunAsync(_backend.SccEnginePath, arguments, null, cancellationToken);

        if (result.WasCancelled)
        {
            return new AnalysisResult
            {
                Success = false,
                Stage = AnalysisStage.Failed,
                Mode = configuration.OutputMode,
                ErrorMessage = "The analysis was cancelled before the HTML report was written.",
                Diagnostics = diagnostics,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                ProjectPath = projectPath,
            };
        }

        if (result.StandardError.Length > 0)
        {
            foreach (var line in SplitLines(result.StandardError))
            {
                if (line.Length > 0)
                {
                    diagnostics.Add(new DiagnosticEntry { Severity = DiagnosticSeverity.Warning, Message = line });
                }
            }
        }

        if (!result.Succeeded)
        {
            return new AnalysisResult
            {
                Success = false,
                Stage = AnalysisStage.Failed,
                Mode = configuration.OutputMode,
                ErrorMessage = BuildEngineErrorMessage(result.ExitCode, result.StandardError),
                Diagnostics = diagnostics,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                ProjectPath = projectPath,
            };
        }

        if (!File.Exists(reportPath))
        {
            return new AnalysisResult
            {
                Success = false,
                Stage = AnalysisStage.Failed,
                Mode = configuration.OutputMode,
                ErrorMessage = "The engine exited successfully but did not produce the report file.",
                Diagnostics = diagnostics,
                ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                ProjectPath = projectPath,
            };
        }

        // Also pull the section data so the app can confirm what was produced.
        SccReportData? data = null;
        try
        {
            var native = SccArgumentBuilder.BuildNativeArguments(configuration, projectPath);
            var dataResult = await _runner.RunAsync(_backend.ExportToolPath, native, null, cancellationToken);
            if (dataResult.Succeeded && dataResult.StandardOutput.Length > 0)
            {
                data = ParseJson(dataResult.StandardOutput);
            }
        }
        catch (JsonException ex)
        {
            // The engine produced something the app could not read. The HTML
            // file is still the deliverable, but the user should be told.
            diagnostics.Add(new DiagnosticEntry
            {
                Severity = DiagnosticSeverity.Warning,
                Message = "The confirmation pass returned data the app could not read: " + ex.Message,
            });
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // The HTML file is the deliverable in this mode; the confirmation
            // pass is optional and must never fail the export.
        }

        return new AnalysisResult
        {
            Success = true,
            Stage = AnalysisStage.Done,
            Mode = configuration.OutputMode,
            Data = data,
            HtmlPath = reportPath,
            Diagnostics = diagnostics,
            ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
            ProjectPath = projectPath,
        };
    }

    private static SccReportData ParseJson(string json)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var data = JsonSerializer.Deserialize<SccReportData>(json, options)
                   ?? throw new JsonException("The backend returned an empty document.");
        return data.Normalize();
    }

    private static string BuildEngineErrorMessage(int exitCode, string stderr)
    {
        var detail = stderr.Trim();
        var suffix = detail.Length > 0 ? "\n\n" + detail : string.Empty;
        return $"The analysis engine stopped with exit code {exitCode}.{suffix}";
    }

    private static IEnumerable<string> SplitLines(string text) =>
        text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
}
