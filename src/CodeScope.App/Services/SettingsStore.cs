using System;
using System.IO;
using System.Text.Json;
using CodeScope.Models;

namespace CodeScope.Services;

/// <summary>
/// Persists user preferences and presets in the per-user application-data
/// folder. The analysed project is never written to.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        // Enums as readable words so a settings file can be hand-edited.
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public string RootDirectory { get; }

    public string SettingsFilePath => Path.Combine(RootDirectory, "settings.json");

    public string PresetsDirectory => Path.Combine(RootDirectory, "presets");

    public SettingsStore()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodeScope"))
    {
    }

    public SettingsStore(string rootDirectory)
    {
        RootDirectory = rootDirectory;
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(PresetsDirectory);
    }

    public AppSettings LoadSettings()
    {
        try
        {
            EnsureDirectories();
            if (!File.Exists(SettingsFilePath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // A corrupt settings file must never block the application.
            return new AppSettings();
        }
    }

    public void SaveSettings(AppSettings settings)
    {
        try
        {
            EnsureDirectories();
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            WriteAtomically(SettingsFilePath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Persisting preferences is best-effort; the run continues.
        }
    }

    public void SavePreset(string name, ReportConfiguration configuration)
    {
        EnsureDirectories();
        var safe = SafeFileName(name);
        var json = JsonSerializer.Serialize(new PresetPayload { Name = name, Configuration = configuration }, JsonOptions);
        WriteAtomically(Path.Combine(PresetsDirectory, safe + ".json"), json);
    }

    public bool LoadPreset(string name, out ReportConfiguration configuration)
    {
        configuration = new ReportConfiguration();
        try
        {
            var path = Path.Combine(PresetsDirectory, SafeFileName(name) + ".json");
            if (!File.Exists(path))
            {
                return false;
            }

            var json = File.ReadAllText(path);
            var payload = JsonSerializer.Deserialize<PresetPayload>(json, JsonOptions);
            configuration = payload?.Configuration ?? new ReportConfiguration();
            return true;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public IReadOnlyList<string> ListPresets()
    {
        try
        {
            if (!Directory.Exists(PresetsDirectory))
            {
                return Array.Empty<string>();
            }

            var names = new System.Collections.Generic.List<string>();
            foreach (var file in Directory.EnumerateFiles(PresetsDirectory, "*.json"))
            {
                names.Add(Path.GetFileNameWithoutExtension(file));
            }

            names.Sort(StringComparer.CurrentCultureIgnoreCase);
            return names;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    public void DeletePreset(string name)
    {
        try
        {
            var path = Path.Combine(PresetsDirectory, SafeFileName(name) + ".json");
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // best effort
        }
    }

    private static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        foreach (var ch in invalid)
        {
            name = name.Replace(ch, '_');
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return "preset";
        }

        return name.Trim();
    }

    private static void WriteAtomically(string path, string contents)
    {
        var temp = path + ".tmp";
        File.WriteAllText(temp, contents);
        File.Move(temp, path, overwrite: true);
    }

    private sealed class PresetPayload
    {
        public string Name { get; set; } = string.Empty;

        public ReportConfiguration Configuration { get; set; } = new();
    }
}
