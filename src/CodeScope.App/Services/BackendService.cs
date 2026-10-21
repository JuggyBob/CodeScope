using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace CodeScope.Services;

/// <summary>Locates and validates the executables bundled with the application.</summary>
public sealed class BackendService
{
    private static readonly Regex VersionRegex = new(
        @"scc\s+version\s+(\S+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>The application's own directory, where <c>backend/</c> lives.</summary>
    public string AppDirectory { get; }

    public string BackendDirectory => Path.Combine(AppDirectory, "backend");

    public string SccEnginePath => Path.Combine(BackendDirectory, SccArgumentBuilder.EngineName);

    public string ExportToolPath => Path.Combine(BackendDirectory, SccArgumentBuilder.ExportToolName);

    public string? SccVersion { get; private set; }

    public BackendService()
        : this(AppContext.BaseDirectory)
    {
    }

    public BackendService(string appDirectory)
    {
        AppDirectory = appDirectory;
    }

    /// <summary>
    /// Confirms both executables are present and runnable. Returns a short
    /// description of anything missing or broken so the UI can show it.
    /// </summary>
    public async Task<string?> ValidateAsync(CancellationToken cancellationToken)
    {
        var problems = new System.Collections.Generic.List<string>();

        if (!File.Exists(SccEnginePath))
        {
            problems.Add($"Missing engine: {SccEnginePath}");
        }

        if (!File.Exists(ExportToolPath))
        {
            problems.Add($"Missing data adapter: {ExportToolPath}");
        }

        if (problems.Count > 0)
        {
            return string.Join("\n", problems);
        }

        var runner = new ProcessRunner();
        var result = await runner.RunAsync(SccEnginePath, new[] { "--version" }, null, cancellationToken);
        if (!result.Succeeded)
        {
            return result.StandardError.Length > 0
                ? $"The bundled engine failed to start: {result.StandardError.Trim()}"
                : $"The bundled engine failed to start (exit code {result.ExitCode}).";
        }

        SccVersion = ParseVersion(result.StandardOutput);
        if (SccVersion == null)
        {
            return "The bundled engine did not report a version.";
        }

        // The adapter reports the same version string; a mismatch means the two
        // executables came from different builds and the GUI must not mix them.
        var adapter = await runner.RunAsync(ExportToolPath, new[] { "--version" }, null, cancellationToken);
        if (!adapter.Succeeded)
        {
            return adapter.StandardError.Length > 0
                ? $"The bundled data adapter failed to start: {adapter.StandardError.Trim()}"
                : $"The bundled data adapter failed to start (exit code {adapter.ExitCode}).";
        }

        var adapterVersion = ParseVersion(adapter.StandardOutput);
        if (adapterVersion != SccVersion)
        {
            return $"Engine version {SccVersion} does not match adapter version {adapterVersion}. " +
                   "Rebuild both from the frozen snapshot.";
        }

        return null;
    }

    private static string? ParseVersion(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return null;
        }

        var match = VersionRegex.Match(output);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Converts an engine path to a native Windows path. The engine reports
    /// locations with forward slashes; Explorer's <c>/select</c> switch silently
    /// falls back to the Desktop when given one, so normalise before invoking it.
    /// </summary>
    public static string ToNativePath(string path)
    {
        return Path.GetFullPath(path.Replace('/', Path.DirectorySeparatorChar));
    }

    public static void OpenInFileExplorer(string path)
    {
        try
        {
            path = ToNativePath(path);

            if (File.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{path}\"",
                    UseShellExecute = false,
                });
            }
            else if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
            }
        }
        catch
        {
            // Best effort convenience action.
        }
    }
}
