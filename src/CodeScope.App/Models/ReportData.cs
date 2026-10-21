using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CodeScope.Models;

/// <summary>
/// The data contract returned by the bundled <c>codescope-export</c> adapter.
/// Every field mirrors a field the adapter emits, and every section object is
/// <c>null</c> when the engine could not produce it.
/// </summary>
public sealed class SccReportData
{
    [JsonPropertyName("sccVersion")]
    public string SccVersion { get; set; } = string.Empty;

    [JsonPropertyName("repoName")]
    public string RepoName { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("generatedAt")]
    public string GeneratedAt { get; set; } = string.Empty;

    [JsonPropertyName("durationMs")]
    public long DurationMs { get; set; }

    [JsonPropertyName("gitAvailable")]
    public bool GitAvailable { get; set; }

    [JsonPropertyName("window")]
    public HistoryWindowInfo? Window { get; set; }

    [JsonPropertyName("totals")]
    public Totals Totals { get; set; } = new();

    [JsonPropertyName("languages")]
    public List<LanguageRow> Languages { get; set; } = new();

    [JsonPropertyName("files")]
    public List<FileRow> Files { get; set; } = new();

    [JsonPropertyName("uloc")]
    public UlocData? Uloc { get; set; }

    [JsonPropertyName("lineLength")]
    public LineLengthData? LineLength { get; set; }

    [JsonPropertyName("hotspots")]
    public HotspotsData? Hotspots { get; set; }

    [JsonPropertyName("coupling")]
    public CouplingData? Coupling { get; set; }

    [JsonPropertyName("authors")]
    public AuthorsData? Authors { get; set; }

    [JsonPropertyName("timeline")]
    public TimelineData? Timeline { get; set; }

    [JsonPropertyName("cocomo")]
    public CocomoData? Cocomo { get; set; }

    [JsonPropertyName("locomo")]
    public LocomoData? Locomo { get; set; }

    /// <summary>
    /// The adapter omits whole sections as <c>null</c>, and a skipped section
    /// can leave a JSON <c>null</c> where a list is expected. Normalise once,
    /// after parsing, so nothing downstream has to null-check a collection.
    /// </summary>
    public SccReportData Normalize()
    {
        Totals ??= new Totals();
        Languages ??= new List<LanguageRow>();
        Files ??= new List<FileRow>();

        // The same thing happens one level down: a section that was only
        // partly produced can carry a null list of its own.
        if (Uloc != null)
        {
            Uloc.PerLanguage ??= new List<UlocLanguageRow>();
        }

        if (LineLength != null)
        {
            LineLength.Buckets ??= new List<LineLengthBucket>();
            LineLength.Outliers ??= new List<LineLengthOutlier>();
        }

        if (Hotspots != null)
        {
            Hotspots.Records ??= new List<HotspotRow>();
        }

        if (Coupling != null)
        {
            Coupling.Pairs ??= new List<CouplingRow>();
        }

        if (Authors != null)
        {
            Authors.Rows ??= new List<AuthorRow>();
            Authors.BusAuthors ??= new List<string>();
        }

        if (Timeline?.Languages != null)
        {
            Timeline.Languages.Rows ??= new List<LangTimelineRow>();
            foreach (var row in Timeline.Languages.Rows)
            {
                row.Deltas ??= new List<long>();
                row.Trajectory ??= new List<long>();
            }
        }

        if (Timeline?.Authors != null)
        {
            Timeline.Authors.Rows ??= new List<AuthorTimelineRow>();
        }

        return this;
    }
}

public sealed class HistoryWindowInfo
{
    [JsonPropertyName("depth")]
    public int Depth { get; set; }

    [JsonPropertyName("commits")]
    public int Commits { get; set; }

    [JsonPropertyName("from")]
    public string? From { get; set; }

    [JsonPropertyName("to")]
    public string? To { get; set; }
}

public sealed class Totals
{
    [JsonPropertyName("files")]
    public long Files { get; set; }

    [JsonPropertyName("lines")]
    public long Lines { get; set; }

    [JsonPropertyName("code")]
    public long Code { get; set; }

    [JsonPropertyName("comment")]
    public long Comment { get; set; }

    [JsonPropertyName("blank")]
    public long Blank { get; set; }

    [JsonPropertyName("complexity")]
    public long Complexity { get; set; }

    [JsonPropertyName("bytes")]
    public long Bytes { get; set; }
}

public sealed class LanguageRow
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("bytes")]
    public long Bytes { get; set; }

    [JsonPropertyName("codeBytes")]
    public long CodeBytes { get; set; }

    [JsonPropertyName("lines")]
    public long Lines { get; set; }

    [JsonPropertyName("code")]
    public long Code { get; set; }

    [JsonPropertyName("comment")]
    public long Comment { get; set; }

    [JsonPropertyName("blank")]
    public long Blank { get; set; }

    [JsonPropertyName("complexity")]
    public long Complexity { get; set; }

    [JsonPropertyName("cognitive")]
    public long Cognitive { get; set; }

    [JsonPropertyName("count")]
    public long Count { get; set; }

    [JsonPropertyName("weightedComplexity")]
    public double WeightedComplexity { get; set; }

    [JsonPropertyName("uloc")]
    public int Uloc { get; set; }

    [JsonPropertyName("filePercent")]
    public double? FilePercent { get; set; }

    [JsonPropertyName("linePercent")]
    public double? LinePercent { get; set; }

    [JsonPropertyName("codePercent")]
    public double? CodePercent { get; set; }

    [JsonPropertyName("commentPercent")]
    public double? CommentPercent { get; set; }

    [JsonPropertyName("blankPercent")]
    public double? BlankPercent { get; set; }

    [JsonPropertyName("complexityPercent")]
    public double? ComplexityPercent { get; set; }

    [JsonPropertyName("bytePercent")]
    public double? BytePercent { get; set; }

    /// <summary>Share of code, used for the distribution bar.</summary>
    public double CodeShare => CodePercent ?? 0.0;
}

public sealed class FileRow
{
    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;

    [JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;

    [JsonPropertyName("extension")]
    public string Extension { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("possibleLanguages")]
    public List<string> PossibleLanguages { get; set; } = new();

    [JsonPropertyName("bytes")]
    public long Bytes { get; set; }

    [JsonPropertyName("lines")]
    public long Lines { get; set; }

    [JsonPropertyName("code")]
    public long Code { get; set; }

    [JsonPropertyName("comment")]
    public long Comment { get; set; }

    [JsonPropertyName("blank")]
    public long Blank { get; set; }

    [JsonPropertyName("complexity")]
    public long Complexity { get; set; }

    [JsonPropertyName("cognitive")]
    public long Cognitive { get; set; }

    [JsonPropertyName("uloc")]
    public int Uloc { get; set; }

    [JsonPropertyName("binary")]
    public bool Binary { get; set; }

    [JsonPropertyName("minified")]
    public bool Minified { get; set; }

    [JsonPropertyName("generated")]
    public bool Generated { get; set; }
}

public sealed class UlocData
{
    [JsonPropertyName("global")]
    public int Global { get; set; }

    [JsonPropertyName("perLanguage")]
    public List<UlocLanguageRow> PerLanguage { get; set; } = new();

    [JsonPropertyName("totalLines")]
    public long TotalLines { get; set; }

    [JsonPropertyName("dryness")]
    public double Dryness { get; set; }
}

public sealed class UlocLanguageRow
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("uloc")]
    public int Uloc { get; set; }
}

public sealed class LineLengthData
{
    [JsonPropertyName("buckets")]
    public List<LineLengthBucket> Buckets { get; set; } = new();

    [JsonPropertyName("mean")]
    public double Mean { get; set; }

    [JsonPropertyName("max")]
    public int Max { get; set; }

    [JsonPropertyName("outliers")]
    public List<LineLengthOutlier> Outliers { get; set; } = new();

    [JsonPropertyName("totalLines")]
    public long TotalLines { get; set; }
}

public sealed class LineLengthBucket
{
    [JsonPropertyName("start")]
    public int Start { get; set; }

    [JsonPropertyName("end")]
    public int End { get; set; }

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public long Count { get; set; }
}

public sealed class LineLengthOutlier
{
    [JsonPropertyName("file")]
    public string File { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("lineLength")]
    public int LineLength { get; set; }
}

public sealed class HotspotsData
{
    [JsonPropertyName("window")]
    public HistoryWindowInfo? Window { get; set; }

    [JsonPropertyName("records")]
    public List<HotspotRow> Records { get; set; } = new();

    [JsonPropertyName("totalRaw")]
    public int TotalRaw { get; set; }

    [JsonPropertyName("available")]
    public bool Available { get; set; }
}

public sealed class HotspotRow
{
    [JsonPropertyName("file")]
    public string File { get; set; } = string.Empty;

    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("complexity")]
    public long Complexity { get; set; }

    [JsonPropertyName("commits")]
    public int Commits { get; set; }

    [JsonPropertyName("linesChanged")]
    public long LinesChanged { get; set; }

    [JsonPropertyName("authors")]
    public int Authors { get; set; }

    [JsonPropertyName("codeChurn")]
    public long CodeChurn { get; set; }

    [JsonPropertyName("commentChurn")]
    public long CommentChurn { get; set; }

    [JsonPropertyName("score")]
    public double Score { get; set; }
}

public sealed class CouplingData
{
    [JsonPropertyName("window")]
    public HistoryWindowInfo? Window { get; set; }

    [JsonPropertyName("pairs")]
    public List<CouplingRow> Pairs { get; set; } = new();

    [JsonPropertyName("totalPairs")]
    public int TotalPairs { get; set; }

    [JsonPropertyName("available")]
    public bool Available { get; set; }
}

public sealed class CouplingRow
{
    [JsonPropertyName("fileA")]
    public string FileA { get; set; } = string.Empty;

    [JsonPropertyName("fileB")]
    public string FileB { get; set; } = string.Empty;

    [JsonPropertyName("shared")]
    public int Shared { get; set; }

    [JsonPropertyName("degree")]
    public double Degree { get; set; }
}

public sealed class AuthorsData
{
    [JsonPropertyName("window")]
    public HistoryWindowInfo? Window { get; set; }

    [JsonPropertyName("rows")]
    public List<AuthorRow> Rows { get; set; } = new();

    [JsonPropertyName("busFactor")]
    public int BusFactor { get; set; }

    [JsonPropertyName("busAuthors")]
    public List<string> BusAuthors { get; set; } = new();

    [JsonPropertyName("busCovered")]
    public double BusCovered { get; set; }

    [JsonPropertyName("inWindowCode")]
    public long InWindowCode { get; set; }
}

public sealed class AuthorRow
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("code")]
    public long Code { get; set; }

    [JsonPropertyName("comment")]
    public long Comment { get; set; }

    [JsonPropertyName("complexity")]
    public long Complexity { get; set; }

    [JsonPropertyName("files")]
    public int Files { get; set; }

    [JsonPropertyName("ownsPercent")]
    public double OwnsPercent { get; set; }

    [JsonPropertyName("inWindowPercent")]
    public double InWindowPercent { get; set; }

    [JsonPropertyName("lastCommit")]
    public string? LastCommit { get; set; }

    [JsonPropertyName("sentinel")]
    public bool Sentinel { get; set; }
}

public sealed class TimelineData
{
    [JsonPropertyName("languages")]
    public LangTimelineData? Languages { get; set; }

    [JsonPropertyName("authors")]
    public AuthorTimelineData? Authors { get; set; }
}

public sealed class LangTimelineData
{
    [JsonPropertyName("window")]
    public HistoryWindowInfo? Window { get; set; }

    [JsonPropertyName("buckets")]
    public int Buckets { get; set; }

    [JsonPropertyName("languages")]
    public List<LangTimelineRow> Rows { get; set; } = new();
}

public sealed class LangTimelineRow
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    [JsonPropertyName("startingLines")]
    public long StartingLines { get; set; }

    [JsonPropertyName("codeNow")]
    public long CodeNow { get; set; }

    [JsonPropertyName("change")]
    public long Change { get; set; }

    [JsonPropertyName("sharePercent")]
    public double SharePercent { get; set; }

    [JsonPropertyName("deltas")]
    public List<long> Deltas { get; set; } = new();

    [JsonPropertyName("trajectory")]
    public List<long> Trajectory { get; set; } = new();
}

public sealed class AuthorTimelineData
{
    [JsonPropertyName("window")]
    public HistoryWindowInfo? Window { get; set; }

    [JsonPropertyName("buckets")]
    public int Buckets { get; set; }

    [JsonPropertyName("authors")]
    public List<AuthorTimelineRow> Rows { get; set; } = new();
}

public sealed class AuthorTimelineRow
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("totalCommits")]
    public int TotalCommits { get; set; }

    [JsonPropertyName("codeDelta")]
    public long CodeDelta { get; set; }

    [JsonPropertyName("series")]
    public List<TimelineBucket> Series { get; set; } = new();
}

public sealed class TimelineBucket
{
    [JsonPropertyName("bucketStart")]
    public string? BucketStart { get; set; }

    [JsonPropertyName("code")]
    public long Code { get; set; }

    [JsonPropertyName("commits")]
    public int Commits { get; set; }

    [JsonPropertyName("codeDelta")]
    public long CodeDelta { get; set; }
}

public sealed class CocomoData
{
    [JsonPropertyName("projectType")]
    public string ProjectType { get; set; } = string.Empty;

    [JsonPropertyName("sumCode")]
    public long SumCode { get; set; }

    [JsonPropertyName("estimatedEffort")]
    public double EstimatedEffort { get; set; }

    [JsonPropertyName("estimatedCost")]
    public double EstimatedCost { get; set; }

    [JsonPropertyName("scheduleMonths")]
    public double ScheduleMonths { get; set; }

    [JsonPropertyName("peopleRequired")]
    public double PeopleRequired { get; set; }

    [JsonPropertyName("averageWage")]
    public long AverageWage { get; set; }

    [JsonPropertyName("overhead")]
    public double Overhead { get; set; }

    [JsonPropertyName("eaf")]
    public double Eaf { get; set; }

    [JsonPropertyName("currencySymbol")]
    public string CurrencySymbol { get; set; } = string.Empty;
}

public sealed class LocomoData
{
    [JsonPropertyName("inputTokens")]
    public double InputTokens { get; set; }

    [JsonPropertyName("outputTokens")]
    public double OutputTokens { get; set; }

    [JsonPropertyName("cost")]
    public double Cost { get; set; }

    [JsonPropertyName("generationSeconds")]
    public double GenerationSeconds { get; set; }

    [JsonPropertyName("reviewHours")]
    public double ReviewHours { get; set; }

    [JsonPropertyName("averageComplexityMultiplier")]
    public double AverageComplexityMultiplier { get; set; }

    [JsonPropertyName("iterationFactor")]
    public double IterationFactor { get; set; }

    [JsonPropertyName("preset")]
    public string Preset { get; set; } = string.Empty;
}
