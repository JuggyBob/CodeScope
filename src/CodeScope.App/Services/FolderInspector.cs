using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CodeScope.Services;

/// <summary>A quick, read-only look at the folder the user picked.</summary>
public sealed record FolderInfo
{
    public string FullPath { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public bool Exists { get; init; }

    public bool IsEmpty { get; init; }

    public bool IsGitRepository { get; init; }

    public long FileCount { get; init; }

    public long TotalBytes { get; init; }

    public string? Error { get; init; }
}

/// <summary>
/// Validates the selected source directory and detects whether it lives inside a
/// Git repository. Detection walks up the directory chain looking for a
/// <c>.git</c> entry (file or directory), which mirrors how scc's own history
/// engine opens a working tree, including linked worktrees.
/// </summary>
public static class FolderInspector
{
    public static FolderInfo Inspect(string path)
    {
        var info = new FolderInfo { FullPath = path };
        if (string.IsNullOrWhiteSpace(path))
        {
            return info with { Error = "No folder selected." };
        }

        try
        {
            var full = Path.GetFullPath(path);
            info = new FolderInfo { FullPath = full };

            if (!Directory.Exists(full))
            {
                return info with { Error = $"This folder does not exist or cannot be reached:\n{full}" };
            }

            var enumerated = Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories).ToList();
            return info with
            {
                Exists = true,
                DisplayName = new DirectoryInfo(full).Name,
                IsEmpty = enumerated.Count == 0,
                FileCount = enumerated.Count,
                TotalBytes = enumerated.Sum(f => SafeLength(f)),
                IsGitRepository = FindGitRoot(full) != null,
            };
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or PathTooLongException or IOException)
        {
            return info with { Error = $"This folder cannot be read:\n{ex.Message}" };
        }
    }

    /// <summary>True when a Git working tree can be opened at (or above) this path.</summary>
    public static bool HasGitRepository(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            return FindGitRoot(Path.GetFullPath(path)) != null;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or PathTooLongException or IOException)
        {
            return false;
        }
    }

    private static string? FindGitRoot(string path)
    {
        var directory = new DirectoryInfo(path);
        while (directory != null)
        {
            var gitEntry = Path.Combine(directory.FullName, ".git");
            if (Directory.Exists(gitEntry) || File.Exists(gitEntry))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static long SafeLength(string file)
    {
        try
        {
            return new FileInfo(file).Length;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or FileNotFoundException or IOException)
        {
            return 0;
        }
    }

    /// <summary>
    /// The folder browsers are limited to a reasonable scope. Very deep or
    /// network-backed trees are still allowed, just flagged.
    /// </summary>
    public static string? WarnIfUnusual(string path, long fileCount, long totalBytes)
    {
        if (fileCount == 0)
        {
            return "The selected folder contains no files. SCC will still run but every metric will be zero.";
        }

        if (fileCount > 200_000)
        {
            return $"This folder contains {fileCount:N0} files. The analysis may take a while and use a lot of memory. " +
                   "Consider narrowing the selection or raising the file limits in Advanced Settings.";
        }

        if (totalBytes > 5L * 1024 * 1024 * 1024)
        {
            return $"The folder holds {totalBytes / (1024d * 1024 * 1024):0.0} GB. Large repositories can take several minutes.";
        }

        return null;
    }
}
