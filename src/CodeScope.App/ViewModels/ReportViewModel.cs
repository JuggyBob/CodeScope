using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CodeScope.Models;
using CodeScope.Services;

namespace CodeScope.ViewModels;

/// <summary>
/// Backs the in-app report dashboard. It renders only the sections the user
/// selected, and clearly marks the ones the engine could not produce.
/// </summary>
public sealed partial class ReportViewModel : ViewModelBase
{
    private readonly ReportConfiguration _configuration;
    private readonly Action _back;
    private string _fileQuery = string.Empty;
    private string _languageFilter = "All languages";

    public ReportViewModel(
        ReportConfiguration configuration,
        SccReportData data,
        Action back,
        Func<Task> exportHtml,
        IReadOnlyList<DiagnosticEntry>? diagnostics = null)
    {
        _configuration = configuration;
        Data = data;
        _back = back;

        // The engine's own notes — a section it could not produce, a folder it
        // had to skip — belong in the report, not in a log nobody reads.
        EngineNotes = diagnostics?
            .Where(d => !string.IsNullOrWhiteSpace(d.Message))
            .Select(d => d.Message.Trim())
            .ToList() ?? new List<string>();

        BuildMetricCards();
        BuildCharts();
        RebuildFiles();
        RebuildLanguageOptions();

        BackCommand = new DelegateCommand(_ => _back());
        ExportHtmlCommand = new DelegateCommand(_ => _ = exportHtml());
        ResetFileFilterCommand = new DelegateCommand(_ => FileQuery = string.Empty);
    }

    /// <summary>What the engine said while it worked, if anything.</summary>
    public IReadOnlyList<string> EngineNotes { get; }

    /// <summary>True when the engine left notes worth showing.</summary>
    public bool HasEngineNotes => EngineNotes.Count > 0;

    // ------------------------------------------------------------------
    // Table sorting
    // ------------------------------------------------------------------

    /// <summary>How each table of the report is sorted.</summary>
    private readonly Dictionary<string, TableSortState> _sortStates = new(StringComparer.Ordinal)
    {
        // The per-file table arrives sorted by the busiest column.
        ["Files"] = new TableSortState("Files", "Lines", "Lines", descending: true),

        // The language table arrives sorted by size, because the engine hands
        // it over that way.
        ["Languages"] = new TableSortState("Languages", "Lines", "Lines", descending: true),

        // The other tables keep the engine's own order until the user sorts
        // them, so no heading claims an order that is not there.
        ["Outliers"] = new TableSortState("Outliers", string.Empty, string.Empty, descending: false),
        ["Hotspots"] = new TableSortState("Hotspots", string.Empty, string.Empty, descending: false),
        ["Coupling"] = new TableSortState("Coupling", string.Empty, string.Empty, descending: false),
        ["Authors"] = new TableSortState("Authors", string.Empty, string.Empty, descending: false),
    };

    /// <summary>
    /// The sort of one table, for the arrow in its column headings. The report
    /// raises <see cref="PropertyChanged"/> for it whenever a table is sorted,
    /// so the headings follow the rows at once.
    /// </summary>
    public TableSortState? SortState(string table) =>
        _sortStates.TryGetValue(table, out var state) ? state : null;

    /// <summary>
    /// Bumped whenever a table is sorted. The column headings bind to it, so
    /// they learn that the order changed even though the table itself did not.
    /// </summary>
    public int SortVersion { get; private set; }

    /// <summary>
    /// Records that a table is now sorted by a column and puts the rows in
    /// that order. The heading is what the user sees, the key the property the
    /// rows are ordered by.
    /// </summary>
    public void SetSortState(string table, string heading, string key, bool descending)
    {
        if (!_sortStates.TryGetValue(table, out var state))
        {
            // A table the report does not know about has no rows to order.
            return;
        }

        state.Set(heading, key, descending);

        SortVersion++;
        OnPropertyChanged(nameof(SortVersion));

        SortTable(table);
    }

    /// <summary>
    /// Sorts a table by the column the user clicked. The same column again
    /// turns the table around; a new column starts with the smallest value on
    /// top.
    /// </summary>
    public void ToggleSort(string table, string heading, string key)
    {
        if (!_sortStates.TryGetValue(table, out var state))
        {
            return;
        }

        var sameColumn = string.Equals(state.Heading, heading, StringComparison.Ordinal);
        SetSortState(table, heading, key, sameColumn && !state.Descending);
    }

    /// <summary>Orders the rows of one table by its current sort.</summary>
    private void SortTable(string table)
    {
        switch (table)
        {
            case "Files":
                RebuildFiles();
                break;
            case "Languages":
                SortLanguages();
                break;
            case "Outliers":
                SortOutliers();
                break;
            case "Hotspots":
                SortHotspots();
                break;
            case "Coupling":
                SortCoupling();
                break;
            case "Authors":
                SortAuthors();
                break;
        }
    }

    /// <summary>Orders the language table by the column the user picked.</summary>
    private void SortLanguages()
    {
        var state = _sortStates["Languages"];
        IEnumerable<LanguageRow> rows = Data.Languages;

        if (state.Key.Length > 0)
        {
            Func<LanguageRow, IComparable?> key = state.Key switch
            {
                "Name" => r => r.Name,
                "Count" => r => r.Count,
                "Lines" => r => r.Lines,
                "Code" => r => r.Code,
                "Comment" => r => r.Comment,
                "Blank" => r => r.Blank,
                "Complexity" => r => r.Complexity,
                "Bytes" => r => r.Bytes,
                _ => r => r.Lines,
            };

            rows = state.Descending
                ? rows.OrderByDescending(key)
                : rows.OrderBy(key);
        }

        Languages.Clear();
        foreach (var row in rows)
        {
            Languages.Add(row);
        }
    }

    public SccReportData Data { get; }

    public ObservableCollection<MetricCard> MetricCards { get; } = new();

    public ObservableCollection<string> LanguageOptions { get; } = new();

    public ObservableCollection<ReportFileRow> Files { get; } = new();

    /// <summary>The language table, kept in step with its own sort.</summary>
    public ObservableCollection<LanguageRow> Languages { get; } = new();

    public string FileQuery
    {
        get => _fileQuery;
        set
        {
            if (SetProperty(ref _fileQuery, value))
            {
                RebuildFiles();
            }
        }
    }

    public string LanguageFilter
    {
        get => _languageFilter;
        set
        {
            if (SetProperty(ref _languageFilter, value))
            {
                RebuildFiles();
            }
        }
    }

    public ICommand BackCommand { get; }
    public ICommand ExportHtmlCommand { get; }
    public ICommand ResetFileFilterCommand { get; }

    /// <summary>Sections the user asked for that the engine could not produce.</summary>
    public ObservableCollection<UnavailableSection> Unavailable { get; } = new();

    public void RefreshUnavailableSections()
    {
        Unavailable.Clear();
        foreach (var toggle in _configuration.Sections.Where(t => t.Enabled))
        {
            var info = ReportSectionInfo.For(toggle.Id);
            if (info.IsCore || IsProduced(toggle.Id))
            {
                continue;
            }

            Unavailable.Add(new UnavailableSection(info.Name, UnavailableReason(toggle.Id, info)));
        }

        OnPropertyChanged(nameof(HasUnavailableSections));
    }

    /// <summary>
    /// Explains why a section the user asked for is missing. A repository that
    /// exists but has too little history is a different case from a folder that
    /// is not a repository at all, and the two need different words.
    /// </summary>
    private string UnavailableReason(ReportSectionId id, ReportSectionInfo info)
    {
        if (!info.RequiresGit)
        {
            return "The engine did not return this section for the selected options.";
        }

        if (!Data.GitAvailable)
        {
            return "This folder is not inside a Git repository, so this section cannot be produced.";
        }

        return id switch
        {
            ReportSectionId.Coupling =>
                "No two files ever changed together in the selected history window, so there is no co-change coupling to show.",
            ReportSectionId.Hotspots =>
                "No file churn was recorded in the selected history window.",
            ReportSectionId.Authors =>
                "No author activity was recorded in the selected history window.",
            ReportSectionId.Timeline =>
                "No commits were found in the selected history window.",
            _ => "The repository's history in the selected window is too small to produce this section.",
        };
    }

    public bool HasUnavailableSections => Unavailable.Count > 0;

    public bool IsProduced(ReportSectionId id)
    {
        return id switch
        {
            ReportSectionId.Overview => Data.Totals.Files > 0 || Data.Languages.Count > 0,
            ReportSectionId.LanguageBreakdown => Data.Languages.Count > 0,
            ReportSectionId.UniqueLines => Data.Uloc is not null && Data.Uloc.Global > 0,
            ReportSectionId.LineLength => Data.LineLength is { Buckets.Count: > 0 },
            ReportSectionId.Hotspots => Data.Hotspots is { Records.Count: > 0 },
            ReportSectionId.Coupling => Data.Coupling is { Pairs.Count: > 0 },
            ReportSectionId.Authors => Data.Authors is { Rows.Count: > 0 },
            ReportSectionId.Timeline => Data.Timeline?.Languages is { Rows.Count: > 0 } ||
                                        Data.Timeline?.Authors is { Rows.Count: > 0 },
            ReportSectionId.Cocomo => Data.Cocomo is not null,
            ReportSectionId.Locomo => Data.Locomo is not null,
            ReportSectionId.Files => Data.Files.Count > 0,
            ReportSectionId.ShareCard => Data.Totals.Files > 0,
            _ => false,
        };
    }

    private void BuildMetricCards()
    {
        MetricCards.Clear();
        var totals = Data.Totals;
        MetricCards.Add(new MetricCard("Files", Format.Number(totals.Files), "analysed files"));
        MetricCards.Add(new MetricCard("Lines", Format.Number(totals.Lines), "total lines"));
        MetricCards.Add(new MetricCard("Code", Format.Number(totals.Code), "source lines of code"));
        MetricCards.Add(new MetricCard("Comments", Format.Number(totals.Comment), "comment lines"));
        MetricCards.Add(new MetricCard("Blanks", Format.Number(totals.Blank), "blank lines"));
        MetricCards.Add(new MetricCard("Complexity", Format.Number(totals.Complexity), "cyclomatic complexity"));
        MetricCards.Add(new MetricCard("Size", Format.Bytes(totals.Bytes, "si"), "on disk"));

        if (Data.Uloc is { } uloc)
        {
            MetricCards.Add(new MetricCard("ULOC", Format.Number(uloc.Global),
                $"{Format.Percent(uloc.Dryness)} DRY"));
        }
    }

    private void RebuildLanguageOptions()
    {
        var names = Data.Languages.Select(l => l.Name).ToList();
        names.Sort(StringComparer.CurrentCultureIgnoreCase);
        LanguageOptions.Clear();
        LanguageOptions.Add("All languages");
        foreach (var name in names)
        {
            LanguageOptions.Add(name);
        }

        LanguageFilter = "All languages";
    }

    private void RebuildFiles()
    {
        // The rows are walked more than once below, so they are materialised
        // once rather than projected again for every step.
        IEnumerable<ReportFileRow> rows = Data.Files.Select(f => new ReportFileRow(f)).ToList();

        if (!string.IsNullOrWhiteSpace(FileQuery))
        {
            var query = FileQuery.Trim();
            rows = rows.Where(r =>
                r.Location.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                r.Filename.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                r.Language.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        if (LanguageFilter.Length > 0 && LanguageFilter != "All languages")
        {
            rows = rows.Where(r => r.Language == LanguageFilter);
        }

        // The keys are the very property names the table's columns sort on,
        // so the column heading and the view always agree.
        var state = _sortStates["Files"];
        var descending = state.Descending;
        rows = state.Key switch
        {
            "Location" => descending
                ? rows.OrderByDescending(r => r.Location)
                : rows.OrderBy(r => r.Location),
            "Language" => descending
                ? rows.OrderByDescending(r => r.Language).ThenByDescending(r => r.Code)
                : rows.OrderBy(r => r.Language).ThenByDescending(r => r.Code),
            "Code" => descending
                ? rows.OrderByDescending(r => r.Code)
                : rows.OrderBy(r => r.Code),
            "Comment" => descending
                ? rows.OrderByDescending(r => r.Comment)
                : rows.OrderBy(r => r.Comment),
            "Blank" => descending
                ? rows.OrderByDescending(r => r.Blank)
                : rows.OrderBy(r => r.Blank),
            "Complexity" => descending
                ? rows.OrderByDescending(r => r.Complexity)
                : rows.OrderBy(r => r.Complexity),
            "Bytes" => descending
                ? rows.OrderByDescending(r => r.Bytes)
                : rows.OrderBy(r => r.Bytes),
            _ => descending
                ? rows.OrderByDescending(r => r.Lines)
                : rows.OrderBy(r => r.Lines),
        };

        Files.Clear();
        foreach (var row in rows)
        {
            Files.Add(row);
        }

        OnPropertyChanged(nameof(FileResultSummary));
    }

    public string FileResultSummary =>
        $"{Files.Count:N0} of {Data.Files.Count:N0} files match the current filter.";
}

/// <summary>A single headline metric.</summary>
public sealed class MetricCard
{
    public MetricCard(string label, string value, string sublabel)
    {
        Label = label;
        Value = value;
        SubLabel = sublabel;
    }

    public string Label { get; }

    public string Value { get; }

    public string SubLabel { get; }
}

/// <summary>A section the user wanted but the engine could not deliver.</summary>
public sealed class UnavailableSection
{
    public UnavailableSection(string name, string reason)
    {
        Name = name;
        Reason = reason;
    }

    public string Name { get; }

    public string Reason { get; }
}

/// <summary>A per-file row as the table wants it.</summary>
public sealed class ReportFileRow
{
    public ReportFileRow(FileRow row)
    {
        Location = row.Location;
        Filename = row.Filename;
        Extension = row.Extension;
        Language = row.Language;
        Bytes = row.Bytes;
        Lines = row.Lines;
        Code = row.Code;
        Comment = row.Comment;
        Blank = row.Blank;
        Complexity = row.Complexity;
        Uloc = row.Uloc;
        Binary = row.Binary;
        Minified = row.Minified;
        Generated = row.Generated;
    }

    public string Location { get; }

    public string Filename { get; }

    public string Extension { get; }

    public string Language { get; }

    public long Bytes { get; }

    public long Lines { get; }

    public long Code { get; }

    public long Comment { get; }

    public long Blank { get; }

    public long Complexity { get; }

    public int Uloc { get; }

    public bool Binary { get; }

    public bool Minified { get; }

    public bool Generated { get; }
}
