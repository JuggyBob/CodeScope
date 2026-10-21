using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CodeScope.Models;

namespace CodeScope.Services;

/// <summary>
/// Keeps a rolling log of finished analyses in the per-user application-data
/// folder: one JSON file per run, newest first, capped so the folder can never
/// grow without bound. The analysed project is never written to.
/// </summary>
public sealed class HistoryStore
{
    /// <summary>How many runs are kept before the oldest are dropped.</summary>
    public const int MaxEntries = 30;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string RootDirectory { get; }

    public string HistoryDirectory => Path.Combine(RootDirectory, "history");

    public HistoryStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodeScope"))
    {
    }

    public HistoryStore(string rootDirectory)
    {
        RootDirectory = rootDirectory;
    }

    /// <summary>Every stored run, newest first. Unreadable files are skipped.</summary>
    public IReadOnlyList<HistoryEntry> Load()
    {
        try
        {
            if (!Directory.Exists(HistoryDirectory))
            {
                return Array.Empty<HistoryEntry>();
            }

            var entries = new List<HistoryEntry>();
            foreach (var file in Directory.EnumerateFiles(HistoryDirectory, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var entry = JsonSerializer.Deserialize<HistoryEntry>(json, JsonOptions);
                    if (entry == null || string.IsNullOrEmpty(entry.Id))
                    {
                        // A file without a usable identifier is damaged: skip it.
                        continue;
                    }

                    if (entry.Data != null)
                    {
                        entry.Data = entry.Data.Normalize();
                    }

                    entries.Add(entry);
                }
                catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or NotSupportedException)
                {
                    // A single damaged file must not hide the rest of the history.
                }
            }

            entries.Sort((a, b) => b.Timestamp.CompareTo(a.Timestamp));
            return entries;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<HistoryEntry>();
        }
    }

    /// <summary>Stores a run and prunes the oldest entries beyond the cap.</summary>
    public void Save(HistoryEntry entry)
    {
        if (string.IsNullOrEmpty(entry.Id))
        {
            entry.Id = NewId(entry.Timestamp);
        }

        if (!IsSafeId(entry.Id))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(HistoryDirectory);
            var json = JsonSerializer.Serialize(entry, JsonOptions);
            WriteAtomically(Path.Combine(HistoryDirectory, entry.Id + ".json"), json);
            Prune();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Keeping history is a convenience; a failure must never break a run.
        }
    }

    /// <summary>Removes one run. Returns false when it was not there.</summary>
    public bool Delete(string id)
    {
        if (!IsSafeId(id))
        {
            return false;
        }

        try
        {
            var path = Path.Combine(HistoryDirectory, id + ".json");
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Removes every stored run.</summary>
    public void Clear()
    {
        try
        {
            if (!Directory.Exists(HistoryDirectory))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(HistoryDirectory, "*.json"))
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // best effort
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // best effort
        }
    }

    private void Prune()
    {
        try
        {
            if (!Directory.Exists(HistoryDirectory))
            {
                return;
            }

            // The identifier of a run begins with its timestamp, so the file
            // names order the log by age without reading a single report.
            var stale = Directory.GetFiles(HistoryDirectory, "*.json")
                .OrderByDescending(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .Skip(MaxEntries);

            foreach (var file in stale)
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // best effort
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // best effort
        }
    }

    /// <summary>
    /// True when an identifier is a plain file name of our own making. It
    /// keeps a damaged or tampered file from reaching outside the history
    /// folder.
    /// </summary>
    private static bool IsSafeId(string? id) =>
        !string.IsNullOrEmpty(id) &&
        id != "." &&
        id != ".." &&
        id.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 &&
        id.IndexOf('/') < 0 &&
        id.IndexOf('\\') < 0;

    /// <summary>A sortable, collision-free identifier for a run.</summary>
    public static string NewId(DateTimeOffset timestamp) =>
        timestamp.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture) + "-" +
        Guid.NewGuid().ToString("N")[..4];

    private static void WriteAtomically(string path, string contents)
    {
        var temp = path + ".tmp";
        try
        {
            File.WriteAllText(temp, contents);
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            // A half-written temporary file must not be left behind.
            try
            {
                File.Delete(temp);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // best effort
            }

            throw;
        }
    }
}
