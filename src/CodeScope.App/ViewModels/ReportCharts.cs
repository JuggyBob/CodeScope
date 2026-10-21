using System;
using System.Collections.ObjectModel;
using System.Windows;
using CodeScope.Models;
using CodeScope.Services;

namespace CodeScope.ViewModels;

/// <summary>Shape types for the native charts. Values are pre-scaled so the XAML stays declarative.</summary>
public sealed class DistributionSegment
{
    /// <summary>
    /// Segment width in the 1000-unit design space the bar is drawn in. A
    /// plain double because the segments live in a StackPanel, not a Grid —
    /// a Viewbox stretches the whole strip to whatever width is available.
    /// </summary>
    public double Width { get; init; }

    public int SeriesIndex { get; init; }

    public string Label { get; init; } = string.Empty;

    public string ToolTipText { get; init; } = string.Empty;
}

public sealed class HistogramBar
{
    public double Height { get; init; }

    public string Label { get; init; } = string.Empty;

    public string CountText { get; init; } = string.Empty;

    public string ToolTipText { get; init; } = string.Empty;
}

public sealed class TimelineBar
{
    public double Height { get; init; }

    public bool Positive { get; init; }
}

public sealed class TimelineRow
{
    public string Language { get; init; } = string.Empty;

    public string ChangeText { get; init; } = string.Empty;

    public string ShareText { get; init; } = string.Empty;

    public string CodeNowText { get; init; } = string.Empty;

    public string TrajectoryText { get; init; } = string.Empty;

    public ObservableCollection<TimelineBar> Bars { get; } = new();
}

public sealed class AuthorTimelineRowView
{
    public string Author { get; init; } = string.Empty;

    public string CommitsText { get; init; } = string.Empty;

    public string DeltaText { get; init; } = string.Empty;

    public ObservableCollection<TimelineBar> Bars { get; } = new();
}

public sealed class HotspotRowView
{
    public string File { get; init; } = string.Empty;

    public string Language { get; init; } = string.Empty;

    public string ComplexityText { get; init; } = string.Empty;

    public string CommitsText { get; init; } = string.Empty;

    public string LinesChangedText { get; init; } = string.Empty;

    public string AuthorsText { get; init; } = string.Empty;

    public string ChurnText { get; init; } = string.Empty;

    public string ScoreText { get; init; } = string.Empty;

    public string Heat { get; init; } = string.Empty;

    // The raw numbers behind the formatted columns. The table sorts on these,
    // so "1,234" never sorts before "999".
    public long Complexity { get; init; }

    public int Commits { get; init; }

    public long LinesChanged { get; init; }

    public int Authors { get; init; }

    public long Churn { get; init; }

    public double Score { get; init; }
}

public sealed class CouplingRowView
{
    public string FileA { get; init; } = string.Empty;

    public string FileB { get; init; } = string.Empty;

    public string SharedText { get; init; } = string.Empty;

    public string DegreeText { get; init; } = string.Empty;

    public double Degree { get; init; }

    /// <summary>The raw number of shared commits, so the table sorts on it.</summary>
    public long Shared { get; init; }
}

public sealed class AuthorRowView
{
    public string Name { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string CodeText { get; init; } = string.Empty;

    public string CommentText { get; init; } = string.Empty;

    public string ComplexityText { get; init; } = string.Empty;

    public string FilesText { get; init; } = string.Empty;

    public string OwnsText { get; init; } = string.Empty;

    public string LastCommitText { get; init; } = string.Empty;

    public bool IsBusAuthor { get; init; }

    // The raw numbers behind the formatted columns, so the table sorts on
    // values rather than on their printed form.
    public long Code { get; init; }

    public long Comment { get; init; }

    public long Complexity { get; init; }

    public int Files { get; init; }

    /// <summary>The share of the code this author owns, in percent.</summary>
    public double Owns { get; init; }

    /// <summary>When the author last committed, so the column sorts by time.</summary>
    public DateTimeOffset LastCommit { get; init; }
}

public sealed class OutlierRowView
{
    public string File { get; init; } = string.Empty;

    public string Language { get; init; } = string.Empty;

    public string LineLengthText { get; init; } = string.Empty;

    /// <summary>The raw character count, so the table sorts on the number.</summary>
    public int LineLength { get; init; }
}

public sealed class UlocLanguageRowView
{
    public string Language { get; init; } = string.Empty;

    public string UlocText { get; init; } = string.Empty;
}

/// <summary>
/// The chart-shaped projections of <see cref="SccReportData"/>: pre-formatted
/// strings and pre-scaled geometry so the view stays pure XAML.
/// </summary>
public sealed partial class ReportViewModel
{
    private const double HistogramChartHeight = 110;
    private const double TimelineChartHeight = 40;

    public ObservableCollection<DistributionSegment> LanguageSegments { get; } = new();

    public ObservableCollection<HistogramBar> HistogramBars { get; } = new();

    public ObservableCollection<TimelineRow> LanguageTimelineRows { get; } = new();

    public ObservableCollection<AuthorTimelineRowView> AuthorTimelineRows { get; } = new();

    public ObservableCollection<MetricCard> CocomoCards { get; } = new();

    public ObservableCollection<MetricCard> LocomoCards { get; } = new();

    public ObservableCollection<HotspotRowView> Hotspots { get; } = new();

    public ObservableCollection<CouplingRowView> Coupling { get; } = new();

    public ObservableCollection<AuthorRowView> Authors { get; } = new();

    public ObservableCollection<OutlierRowView> Outliers { get; } = new();

    public ObservableCollection<UlocLanguageRowView> UlocLanguages { get; } = new();

    public bool HasAuthorTimeline => AuthorTimelineRows.Count > 0;

    public bool ShowAuthorTimeline => ShowTimeline && HasAuthorTimeline;

    // ---- section visibility: requested AND produced --------------------

    public bool ShowUniqueLines => ShowSection(ReportSectionId.UniqueLines);

    public bool ShowLineLength => ShowSection(ReportSectionId.LineLength);

    public bool ShowHotspots => ShowSection(ReportSectionId.Hotspots);

    public bool ShowCoupling => ShowSection(ReportSectionId.Coupling);

    public bool ShowAuthors => ShowSection(ReportSectionId.Authors);

    public bool ShowTimeline => ShowSection(ReportSectionId.Timeline);

    public bool ShowCocomo => ShowSection(ReportSectionId.Cocomo);

    public bool ShowLocomo => ShowSection(ReportSectionId.Locomo);

    public bool ShowFiles => ShowSection(ReportSectionId.Files);

    public bool ShowUlocDetail => Data.Uloc is { PerLanguage.Count: > 0 };

    public bool HasOutliers => Data.LineLength is { Outliers.Count: > 0 };

    private bool ShowSection(ReportSectionId id) => _configuration.IsEnabled(id) && IsProduced(id);

    // ---- formatted headline facts ---------------------------------------

    public string GeneratedAtText => Format.DateTime(Data.GeneratedAt);

    public string DurationText => Data.DurationMs < 1000
        ? $"{Data.DurationMs} ms"
        : $"{Data.DurationMs / 1000d:0.0} s";

    public string EngineText => $"scc {Data.SccVersion}";

    public string WindowText => Data.Window is { } w
        ? $"{Format.Number(w.Commits)} commits · {Format.DateTime(w.From)} → {Format.DateTime(w.To)}"
        : string.Empty;

    public string UlocSummaryText => Data.Uloc is { } uloc
        ? $"{Format.Number(uloc.Global)} unique lines · {Format.Percent(uloc.Dryness)} DRY"
        : string.Empty;

    public string BusFactorText => Data.Authors is { } authors
        ? $"bus factor {authors.BusFactor}" +
          (authors.BusAuthors.Count > 0 ? $" · {authors.BusAuthors[0]} owns the most" : string.Empty)
        : string.Empty;

    public string CostSummaryText => Data.Cocomo is { } cocomo
        ? $"est. cost {Format.Currency(cocomo.EstimatedCost, cocomo.CurrencySymbol)}"
        : string.Empty;

    // ---- COCOMO / LOCOMO -------------------------------------------------

    public string CocomoEffortText => Data.Cocomo is { } c
        ? $"{Format.Fixed1(c.EstimatedEffort)} person-months · {c.ProjectType}" : string.Empty;

    public string CocomoCostText => Data.Cocomo is { } c
        ? Format.Currency(c.EstimatedCost, c.CurrencySymbol) : string.Empty;

    public string CocomoCostSubText => Data.Cocomo is { } c
        ? $"avg {Format.Currency(c.AverageWage, c.CurrencySymbol)}/yr · ×{Format.Fixed1(c.Overhead)} overhead · EAF {Format.Fixed2(c.Eaf)}" : string.Empty;

    public string CocomoScheduleText => Data.Cocomo is { } c
        ? $"{Format.Fixed1(c.ScheduleMonths)} months" : string.Empty;

    public string CocomoPeopleText => Data.Cocomo is { } c
        ? $"{Format.Fixed1(c.PeopleRequired)} people" : string.Empty;

    public string LocomoCostText => Data.Locomo is { } l
        ? Format.Currency(l.Cost, Data.Cocomo?.CurrencySymbol ?? "$") : string.Empty;

    public string LocomoTokensText => Data.Locomo is { } l
        ? $"{Format.Compact(l.InputTokens)} in / {Format.Compact(l.OutputTokens)} out tokens" : string.Empty;

    public string LocomoTimeText => Data.Locomo is { } l
        ? $"{Format.Seconds(l.GenerationSeconds)} to regenerate" : string.Empty;

    public string LocomoReviewText => Data.Locomo is { } l
        ? $"{Format.ReviewHours(l.ReviewHours)} human review" : string.Empty;

    public string LocomoPresetText => Data.Locomo is { } l ? $"preset {l.Preset}" : string.Empty;

    // ---- chart construction ----------------------------------------------

    private void BuildCharts()
    {
        BuildLanguageDistribution();
        BuildHistogram();
        BuildTimelines();
        BuildAuthorTimeline();
        BuildCostCards();
        BuildHistoryTables();
        SortLanguages();
        RefreshUnavailableSections();
    }

    private void BuildLanguageDistribution()
    {
        LanguageSegments.Clear();
        var total = 0d;
        foreach (var language in Data.Languages)
        {
            total += language.Code;
        }

        if (total <= 0)
        {
            return;
        }

        var index = 0;
        foreach (var language in Data.Languages)
        {
            var share = language.Code / total;
            LanguageSegments.Add(new DistributionSegment
            {
                // A floor keeps a tiny language visible instead of sub-pixel.
                Width = Math.Max(share, 0.004) * 1000,
                SeriesIndex = index++,
                Label = language.Name,
                ToolTipText = $"{language.Name}: {Format.Number(language.Code)} code lines ({Format.Percent(share)})",
            });
        }
    }

    private void BuildHistogram()
    {
        HistogramBars.Clear();
        if (Data.LineLength is not { } lineLength)
        {
            return;
        }

        long max = 1;
        foreach (var bucket in lineLength.Buckets)
        {
            if (bucket.Count > max)
            {
                max = bucket.Count;
            }
        }

        foreach (var bucket in lineLength.Buckets)
        {
            HistogramBars.Add(new HistogramBar
            {
                Height = Math.Max(2d, bucket.Count * HistogramChartHeight / max),
                Label = bucket.Label,
                CountText = Format.Number(bucket.Count),
                ToolTipText = $"{bucket.Label} characters: {Format.Number(bucket.Count)} lines",
            });
        }

        Outliers.Clear();
        if (Data.LineLength is { Outliers: { } outliers })
        {
            foreach (var outlier in outliers)
            {
                Outliers.Add(new OutlierRowView
                {
                    File = outlier.File,
                    Language = outlier.Language,
                    LineLengthText = Format.Number(outlier.LineLength),
                    LineLength = outlier.LineLength,
                });
            }
        }

        OnPropertyChanged(nameof(HasOutliers));
    }

    private void BuildTimelines()
    {
        LanguageTimelineRows.Clear();
        if (Data.Timeline?.Languages is not { } timeline)
        {
            return;
        }

        foreach (var row in timeline.Rows)
        {
            // Each language is scaled against its own peak so a small language
            // still shows its shape; the printed numbers remain exact.
            long rowMax = 1;
            foreach (var delta in row.Deltas)
            {
                var magnitude = Math.Abs(delta);
                if (magnitude > rowMax)
                {
                    rowMax = magnitude;
                }
            }

            var view = new TimelineRow
            {
                Language = row.Language,
                ChangeText = (row.Change >= 0 ? "+" : string.Empty) + Format.Number(row.Change),
                ShareText = Format.Percent(row.SharePercent),
                CodeNowText = Format.Number(row.CodeNow),
                TrajectoryText = $"{Format.Number(row.StartingLines)} → {Format.Number(row.CodeNow)} lines",
            };

            foreach (var delta in row.Deltas)
            {
                view.Bars.Add(new TimelineBar
                {
                    Height = Math.Max(1.5d, Math.Abs(delta) * TimelineChartHeight / rowMax),
                    Positive = delta >= 0,
                });
            }

            LanguageTimelineRows.Add(view);
        }
    }

    private void BuildAuthorTimeline()
    {
        AuthorTimelineRows.Clear();
        if (Data.Timeline?.Authors is not { } timeline)
        {
            OnPropertyChanged(nameof(HasAuthorTimeline));
            return;
        }

        long maxAbs = 1;
        foreach (var row in timeline.Rows)
        {
            foreach (var bucket in row.Series)
            {
                var magnitude = Math.Abs(bucket.CodeDelta);
                if (magnitude > maxAbs)
                {
                    maxAbs = magnitude;
                }
            }
        }

        foreach (var row in timeline.Rows)
        {
            var view = new AuthorTimelineRowView
            {
                Author = row.Name,
                CommitsText = $"{Format.Number(row.TotalCommits)} commits",
                DeltaText = (row.CodeDelta >= 0 ? "+" : string.Empty) + Format.Number(row.CodeDelta) + " lines",
            };

            foreach (var bucket in row.Series)
            {
                view.Bars.Add(new TimelineBar
                {
                    // The division is done in floating point: both numbers are
                    // whole lines, and dividing them as integers would throw
                    // away the fraction the bar height depends on.
                    Height = Math.Max(1.5d, Math.Abs(bucket.CodeDelta) * 34d / maxAbs),
                    Positive = bucket.CodeDelta >= 0,
                });
            }

            AuthorTimelineRows.Add(view);
        }

        OnPropertyChanged(nameof(HasAuthorTimeline));
    }

    private void BuildCostCards()
    {
        CocomoCards.Clear();
        LocomoCards.Clear();

        if (Data.Cocomo is { } cocomo)
        {
            CocomoCards.Add(new MetricCard("Effort", Format.Fixed1(cocomo.EstimatedEffort),
                $"person-months · {cocomo.ProjectType}"));
            CocomoCards.Add(new MetricCard("Cost to develop",
                Format.Currency(cocomo.EstimatedCost, cocomo.CurrencySymbol),
                $"avg {Format.Currency(cocomo.AverageWage, cocomo.CurrencySymbol)}/yr · ×{Format.Fixed1(cocomo.Overhead)} overhead"));
            CocomoCards.Add(new MetricCard("Schedule",
                $"{Format.Fixed1(cocomo.ScheduleMonths)} months",
                $"EAF {Format.Fixed2(cocomo.Eaf)} · nominal is 1.00"));
            CocomoCards.Add(new MetricCard("People required",
                Format.Fixed1(cocomo.PeopleRequired),
                $"derived from {Format.Number(cocomo.SumCode)} lines of code"));
        }

        if (Data.Locomo is { } locomo)
        {
            var symbol = Data.Cocomo?.CurrencySymbol ?? "$";
            LocomoCards.Add(new MetricCard("Regeneration cost",
                Format.Currency(locomo.Cost, symbol),
                $"{locomo.Preset} preset"));
            LocomoCards.Add(new MetricCard("Tokens",
                Format.Compact(locomo.InputTokens),
                $"{Format.Compact(locomo.OutputTokens)} output tokens"));
            LocomoCards.Add(new MetricCard("Machine time",
                Format.Seconds(locomo.GenerationSeconds),
                $"{Format.Fixed1(locomo.IterationFactor)}× iteration factor"));
            LocomoCards.Add(new MetricCard("Human review",
                Format.ReviewHours(locomo.ReviewHours),
                $"avg complexity ×{Format.Fixed1(locomo.AverageComplexityMultiplier)}"));
        }
    }

    private void BuildHistoryTables()
    {
        Hotspots.Clear();
        if (Data.Hotspots is { } hotspots)
        {
            foreach (var row in hotspots.Records)
            {
                Hotspots.Add(new HotspotRowView
                {
                    File = row.File,
                    Language = row.Language,
                    ComplexityText = Format.Number(row.Complexity),
                    CommitsText = Format.Number(row.Commits),
                    LinesChangedText = Format.Number(row.LinesChanged),
                    AuthorsText = Format.Number(row.Authors),
                    ChurnText = Format.Number(row.CodeChurn),
                    ScoreText = Format.Fixed1(row.Score),
                    Complexity = row.Complexity,
                    Commits = row.Commits,
                    LinesChanged = row.LinesChanged,
                    Authors = row.Authors,
                    Churn = row.CodeChurn,
                    Score = row.Score,
                });
            }
        }

        Coupling.Clear();
        if (Data.Coupling is { } coupling)
        {
            foreach (var pair in coupling.Pairs)
            {
                Coupling.Add(new CouplingRowView
                {
                    FileA = pair.FileA,
                    FileB = pair.FileB,
                    SharedText = Format.Number(pair.Shared),
                    // The engine already expresses degree as a percentage
                    // (report_template: printf "%.1f%%" .Degree).
                    DegreeText = Format.Fixed1(pair.Degree) + "%",
                    Degree = pair.Degree,
                    Shared = pair.Shared,
                });
            }
        }

        Authors.Clear();
        if (Data.Authors is { } authors)
        {
            foreach (var row in authors.Rows)
            {
                Authors.Add(new AuthorRowView
                {
                    Name = row.Name,
                    Email = row.Email,
                    CodeText = Format.Number(row.Code),
                    CommentText = Format.Number(row.Comment),
                    ComplexityText = Format.Number(row.Complexity),
                    FilesText = Format.Number(row.Files),
                    // The engine already expresses these as percentages
                    // (report_template: printf "%.1f%%" .OwnsPercent).
                    OwnsText = row.Sentinel ? "-" : Format.Fixed1(row.OwnsPercent) + "%",
                    LastCommitText = Format.DateTime(row.LastCommit),
                    IsBusAuthor = authors.BusAuthors.Contains(row.Name),
                    Code = row.Code,
                    Comment = row.Comment,
                    Complexity = row.Complexity,
                    Files = row.Files,
                    Owns = row.Sentinel ? 0d : row.OwnsPercent,
                    LastCommit = Format.CommitDate(row.LastCommit),
                });
            }
        }

        UlocLanguages.Clear();
        if (Data.Uloc is { PerLanguage: { } perLanguage })
        {
            foreach (var row in perLanguage)
            {
                UlocLanguages.Add(new UlocLanguageRowView
                {
                    Language = row.Language,
                    UlocText = Format.Number(row.Uloc),
                });
            }
        }
    }

    // ---- table sorting --------------------------------------------------

    /// <summary>
    /// Puts the rows of a table in order. The report orders every table
    /// itself, so the arrow in a heading and the order of the rows can never
    /// disagree.
    /// </summary>
    private static void ApplySort<T>(ObservableCollection<T> rows, Func<T, IComparable?> key, bool descending)
    {
        var ordered = descending
            ? rows.OrderByDescending(key)
            : rows.OrderBy(key);

        var sorted = ordered.ToList();
        rows.Clear();
        foreach (var row in sorted)
        {
            rows.Add(row);
        }
    }

    private void SortOutliers()
    {
        var state = _sortStates["Outliers"];
        if (state.Key.Length == 0)
        {
            return;
        }

        Func<OutlierRowView, IComparable?> key = state.Key switch
        {
            "Language" => r => r.Language,
            "LineLength" => r => r.LineLength,
            _ => r => r.File,
        };

        ApplySort(Outliers, key, state.Descending);
    }

    private void SortHotspots()
    {
        var state = _sortStates["Hotspots"];
        if (state.Key.Length == 0)
        {
            return;
        }

        Func<HotspotRowView, IComparable?> key = state.Key switch
        {
            "Language" => r => r.Language,
            "Complexity" => r => r.Complexity,
            "Commits" => r => r.Commits,
            "LinesChanged" => r => r.LinesChanged,
            "Authors" => r => r.Authors,
            "Churn" => r => r.Churn,
            "Score" => r => r.Score,
            _ => r => r.File,
        };

        ApplySort(Hotspots, key, state.Descending);
    }

    private void SortCoupling()
    {
        var state = _sortStates["Coupling"];
        if (state.Key.Length == 0)
        {
            return;
        }

        Func<CouplingRowView, IComparable?> key = state.Key switch
        {
            "FileB" => r => r.FileB,
            "Shared" => r => r.Shared,
            "Degree" => r => r.Degree,
            _ => r => r.FileA,
        };

        ApplySort(Coupling, key, state.Descending);
    }

    private void SortAuthors()
    {
        var state = _sortStates["Authors"];
        if (state.Key.Length == 0)
        {
            return;
        }

        Func<AuthorRowView, IComparable?> key = state.Key switch
        {
            "Email" => r => r.Email,
            "Code" => r => r.Code,
            "Comment" => r => r.Comment,
            "Complexity" => r => r.Complexity,
            "Files" => r => r.Files,
            "Owns" => r => r.Owns,
            "LastCommit" => r => r.LastCommit,
            _ => r => r.Name,
        };

        ApplySort(Authors, key, state.Descending);
    }
}
