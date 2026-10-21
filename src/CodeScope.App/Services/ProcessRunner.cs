using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CodeScope.Services;

/// <summary>The outcome of one backend invocation.</summary>
public sealed class ProcessResult
{
    public int ExitCode { get; init; }

    public string StandardOutput { get; init; } = string.Empty;

    public string StandardError { get; init; } = string.Empty;

    public long ElapsedMilliseconds { get; init; }

    public bool WasCancelled { get; init; }

    public bool Succeeded => ExitCode == 0 && !WasCancelled;
}

/// <summary>
/// Starts the bundled engine as a real child process. Arguments are passed
/// through <see cref="ProcessStartInfo.ArgumentList"/>, never through a shell,
/// so paths containing spaces, quotes or Unicode survive untouched.
/// </summary>
public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        CancellationToken cancellationToken);
}

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        string? workingDirectory,
        CancellationToken cancellationToken)
    {
        var start = Stopwatch.GetTimestamp();
        var info = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        if (!string.IsNullOrEmpty(workingDirectory))
        {
            info.WorkingDirectory = workingDirectory;
        }

        using var process = new Process { StartInfo = info, EnableRaisingEvents = true };

        if (!process.Start())
        {
            return new ProcessResult { ExitCode = -1, WasCancelled = false };
        }

        // Drain both streams concurrently: a large report can fill either pipe's
        // buffer while the other one is still being written.
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        // A token that is already cancelled must never produce a result: check
        // before waiting so a fast process cannot slip through as "finished".
        if (cancellationToken.IsCancellationRequested)
        {
            Kill(process);
            await Task.WhenAny(stdout, stderr);
            return new ProcessResult
            {
                ExitCode = -1,
                StandardOutput = await SafeRead(stdout),
                StandardError = await SafeRead(stderr),
                ElapsedMilliseconds = ElapsedMs(start),
                WasCancelled = true,
            };
        }

        using (cancellationToken.Register(() => Kill(process)))
        {
            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                Kill(process);
                await Task.WhenAny(stdout, stderr);
                return new ProcessResult
                {
                    ExitCode = process.HasExited ? process.ExitCode : -1,
                    StandardOutput = await SafeRead(stdout),
                    StandardError = await SafeRead(stderr),
                    ElapsedMilliseconds = ElapsedMs(start),
                    WasCancelled = true,
                };
            }
        }

        var outText = await stdout;
        var errText = await stderr;

        return new ProcessResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = outText,
            StandardError = errText,
            ElapsedMilliseconds = ElapsedMs(start),
            WasCancelled = false,
        };
    }

    private static async Task<string> SafeRead(Task<string> read)
    {
        try
        {
            return await read;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort: the process may already have gone away.
        }
    }

    private static long ElapsedMs(long startTimestamp) =>
        (long)(Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds);
}
