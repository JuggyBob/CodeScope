// SPDX-License-Identifier: MIT
//
// codescope-export — CodeScope's data adapter over the frozen scc backend.
//
// This file is NOT part of upstream scc. It is added by CodeScope under
// vendor/scc-source/tools/ and compiled from the frozen snapshot in exactly the
// same step that produces ../backend/scc.exe. Nothing upstream is edited: the
// file only *calls* the frozen engine.
//
// Why it exists
// ------------
// scc's own --report path renders every infographic section into one
// self-contained HTML document. The CodeScope GUI has to present the same
// sections natively (WPF) without ever writing a temporary HTML file, and the
// CLI's structured outputs do not cover everything: --format json/json2 drops
// the line-length histogram and the per-language ULOC rollup, and none of the
// JSON formats exposes the git-history tables (hotspots, coupling, authors,
// timelines). So instead of approximating, this adapter drives
// processor.CollectReportData — the very same function scc's --report calls —
// and prints the resulting ReportData as JSON. Every number the GUI shows in
// native mode therefore comes from the frozen engine's own code path, not from
// a re-implementation.
//
// Usage
// -----
//
//	codescope-export --out report.json [scc analysis flags...] <path>
//
// Every flag mirrors the upstream scc flag of the same name (see --help), so
// the GUI builds one argument list and can hand it to either engine. Flags
// that only steer the CLI's own rendering (--format, --output, --report,
// --mcp, --config, ...) are intentionally absent.

package main

import (
	"encoding/json"
	"flag"
	"fmt"
	"io"
	"os"
	"runtime"
	"strings"
	"time"

	"github.com/boyter/scc/v4/processor"
)

// ---------------------------------------------------------------------------
// Output contract
//
// The JSON written to --out is a single object:
//
//	{
//	  "sccVersion": "4.1.0",
//	  "repoName":   "scc",
//	  "path":       "C:/src/scc",
//	  "generatedAt":" "2026-10-18T12:00:00Z",
//	  "durationMs": 1234,
//	  "gitAvailable": true,
//	  "window": {"depth":1000,"commits":42,"from":"...","to":"..."},
//	  "totals": {...},
//	  "languages": [...],
//	  "files": [...],
//	  "uloc": {...} | null,
//	  "lineLength": {...} | null,
//	  "hotspots": {...} | null,
//	  "coupling": {...} | null,
//	  "authors":  {...} | null,
//	  "timeline": {"languages":{...}|null,"authors":{...}|null},
//	  "cocomo": {...} | null,
//	  "locomo": {...} | null
//	}
//
// A section object is present-but-empty when the section was not requested;
// it is null when the analysis could not produce it (no Git repository for
// the history sections). The GUI treats "present but empty" as "user turned
// it off" and null as "not available for this project".
// ---------------------------------------------------------------------------

type jsonWindow struct {
	Depth   int    `json:"depth"`
	Commits int    `json:"commits"`
	From    string `json:"from,omitempty"`
	To      string `json:"to,omitempty"`
}

type jsonBucket struct {
	Start int    `json:"start"`
	End   int    `json:"end"`
	Label string `json:"label"`
	Count int64  `json:"count"`
}

type jsonOutlier struct {
	File       string `json:"file"`
	Language   string `json:"language"`
	LineLength int    `json:"lineLength"`
}

type jsonTotals struct {
	Files      int64 `json:"files"`
	Lines      int64 `json:"lines"`
	Code       int64 `json:"code"`
	Comment    int64 `json:"comment"`
	Blank      int64 `json:"blank"`
	Complexity int64 `json:"complexity"`
	Bytes      int64 `json:"bytes"`
}

type jsonPercentages struct {
	FilePercent        *float64 `json:"filePercent,omitempty"`
	LinePercent        *float64 `json:"linePercent,omitempty"`
	CodePercent        *float64 `json:"codePercent,omitempty"`
	CommentPercent     *float64 `json:"commentPercent,omitempty"`
	BlankPercent       *float64 `json:"blankPercent,omitempty"`
	ComplexityPercent  *float64 `json:"complexityPercent,omitempty"`
	BytePercent        *float64 `json:"bytePercent,omitempty"`
}

type jsonLanguage struct {
	Name               string   `json:"name"`
	Bytes              int64    `json:"bytes"`
	CodeBytes          int64    `json:"codeBytes"`
	Lines              int64    `json:"lines"`
	Code               int64    `json:"code"`
	Comment            int64    `json:"comment"`
	Blank              int64    `json:"blank"`
	Complexity         int64    `json:"complexity"`
	Cognitive          int64    `json:"cognitive,omitempty"`
	Count              int64    `json:"count"`
	WeightedComplexity  float64  `json:"weightedComplexity"`
	ULOC               int      `json:"uloc"`
	jsonPercentages    `json:",inline"`
}

type jsonFile struct {
	Location         string   `json:"location"`
	Filename         string   `json:"filename"`
	Extension        string   `json:"extension"`
	Language         string   `json:"language"`
	PossibleLanguage []string `json:"possibleLanguages,omitempty"`
	Bytes            int64    `json:"bytes"`
	Lines            int64    `json:"lines"`
	Code             int64    `json:"code"`
	Comment          int64    `json:"comment"`
	Blank            int64    `json:"blank"`
	Complexity       int64    `json:"complexity"`
	Cognitive        int64    `json:"cognitive,omitempty"`
	Uloc             int      `json:"uloc"`
	Binary           bool     `json:"binary,omitempty"`
	Minified         bool     `json:"minified,omitempty"`
	Generated        bool     `json:"generated,omitempty"`
}

type jsonUlocLanguage struct {
	Language string `json:"language"`
	ULOC     int    `json:"uloc"`
}

type jsonUloc struct {
	Global      int               `json:"global"`
	PerLanguage []jsonUlocLanguage `json:"perLanguage"`
	TotalLines  int64             `json:"totalLines"`
	Dryness     float64           `json:"dryness"`
}

type jsonLineLength struct {
	Buckets    []jsonBucket   `json:"buckets"`
	Mean       float64        `json:"mean"`
	Max        int            `json:"max"`
	Outliers   []jsonOutlier  `json:"outliers"`
	TotalLines int64          `json:"totalLines"`
}

type jsonHotspotRow struct {
	File         string  `json:"file"`
	Language     string  `json:"language"`
	Complexity   int64   `json:"complexity"`
	Commits      int     `json:"commits"`
	LinesChanged int64   `json:"linesChanged"`
	Authors      int     `json:"authors"`
	CodeChurn    int64   `json:"codeChurn"`
	CommentChurn int64   `json:"commentChurn"`
	Score        float64 `json:"score"`
}

type jsonHotspots struct {
	Window    jsonWindow      `json:"window"`
	Records   []jsonHotspotRow `json:"records"`
	TotalRaw  int             `json:"totalRaw"`
	Available bool            `json:"available"`
}

type jsonCouplingRow struct {
	FileA  string  `json:"fileA"`
	FileB  string  `json:"fileB"`
	Shared int     `json:"shared"`
	Degree float64 `json:"degree"`
}

type jsonCoupling struct {
	Window     jsonWindow       `json:"window"`
	Pairs      []jsonCouplingRow `json:"pairs"`
	TotalPairs int              `json:"totalPairs"`
	Available  bool             `json:"available"`
}

type jsonAuthorRow struct {
	Name            string  `json:"name"`
	Email           string  `json:"email"`
	Code            int64   `json:"code"`
	Comment         int64   `json:"comment"`
	Complexity      int64   `json:"complexity"`
	Files           int     `json:"files"`
	OwnsPercent     float64 `json:"ownsPercent"`
	InWindowPercent float64 `json:"inWindowPercent"`
	LastCommit      string  `json:"lastCommit,omitempty"`
	Sentinel        bool    `json:"sentinel"`
}

type jsonAuthors struct {
	Window       jsonWindow     `json:"window"`
	Rows         []jsonAuthorRow `json:"rows"`
	BusFactor    int            `json:"busFactor"`
	BusAuthors   []string       `json:"busAuthors"`
	BusCovered   float64        `json:"busCovered"`
	InWindowCode int64          `json:"inWindowCode"`
}

type jsonTimelineBucket struct {
	BucketStart string `json:"bucketStart"`
	Code        int64  `json:"code"`
	Commits     int    `json:"commits"`
	CodeDelta   int64  `json:"codeDelta"`
}

type jsonLangTimelineRow struct {
	Language      string              `json:"language"`
	StartingLines int64               `json:"startingLines"`
	CodeNow       int64               `json:"codeNow"`
	Change        int64               `json:"change"`
	SharePercent  float64             `json:"sharePercent"`
	Deltas        []int64             `json:"deltas"`
	Trajectory    []int64             `json:"trajectory"`
	Series        []jsonTimelineBucket `json:"series"`
}

type jsonLangTimeline struct {
	Window  jsonWindow            `json:"window"`
	Buckets int                    `json:"buckets"`
	Rows    []jsonLangTimelineRow  `json:"languages"`
}

type jsonAuthorTimelineRow struct {
	Name         string              `json:"name"`
	Email        string              `json:"email"`
	TotalCommits int                 `json:"totalCommits"`
	CodeDelta    int64               `json:"codeDelta"`
	Series       []jsonTimelineBucket `json:"series"`
}

type jsonAuthorTimeline struct {
	Window  jsonWindow             `json:"window"`
	Buckets int                     `json:"buckets"`
	Rows    []jsonAuthorTimelineRow `json:"authors"`
}

type jsonTimeline struct {
	Languages *jsonLangTimeline   `json:"languages"`
	Authors   *jsonAuthorTimeline `json:"authors"`
}

type jsonCocomo struct {
	ProjectType     string  `json:"projectType"`
	SumCode         int64   `json:"sumCode"`
	EstimatedEffort float64 `json:"estimatedEffort"`
	EstimatedCost   float64 `json:"estimatedCost"`
	ScheduleMonths  float64 `json:"scheduleMonths"`
	PeopleRequired  float64 `json:"peopleRequired"`
	AverageWage     int64   `json:"averageWage"`
	Overhead        float64 `json:"overhead"`
	EAF             float64 `json:"eaf"`
	CurrencySymbol  string  `json:"currencySymbol"`
}

type jsonLocomo struct {
	InputTokens           float64 `json:"inputTokens"`
	OutputTokens          float64 `json:"outputTokens"`
	Cost                  float64 `json:"cost"`
	GenerationSeconds     float64 `json:"generationSeconds"`
	ReviewHours           float64 `json:"reviewHours"`
	AverageComplexityMult float64 `json:"averageComplexityMultiplier"`
	IterationFactor       float64 `json:"iterationFactor"`
	Preset                string  `json:"preset"`
}

type reportOutput struct {
	SCCVersion   string           `json:"sccVersion"`
	RepoName     string           `json:"repoName"`
	Path         string           `json:"path"`
	GeneratedAt  string           `json:"generatedAt"`
	DurationMs   int64            `json:"durationMs"`
	GitAvailable bool             `json:"gitAvailable"`
	Window       *jsonWindow      `json:"window,omitempty"`
	Totals       jsonTotals       `json:"totals"`
	Languages    []jsonLanguage   `json:"languages"`
	Files        []jsonFile       `json:"files"`
	ULOC         *jsonUloc        `json:"uloc"`
	LineLength   *jsonLineLength  `json:"lineLength"`
	Hotspots     *jsonHotspots    `json:"hotspots"`
	Coupling     *jsonCoupling    `json:"coupling"`
	Authors      *jsonAuthors     `json:"authors"`
	Timeline     *jsonTimeline    `json:"timeline"`
	Cocomo       *jsonCocomo      `json:"cocomo"`
	Locomo       *jsonLocomo      `json:"locomo"`
}

// ---------------------------------------------------------------------------
// Flag surface — mirrors upstream scc so the GUI can build one argument list
// for both engines.
// ---------------------------------------------------------------------------

// defaultExcludeDirs / defaultExcludeFiles / defaultGeneratedMarkers are the
// built-in safety net scc merges into --exclude-dir / --exclude-file /
// --generated-markers (config.go: mergeSliceDefault). The adapter reproduces
// that merge because CollectReportData reads the merged values.
var (
	defaultExcludeDirs      = []string{".git", ".hg", ".svn"}
	defaultExcludeFiles     = []string{"package-lock.json", "Cargo.lock", "yarn.lock", "pubspec.lock", "Podfile.lock", "pnpm-lock.yaml"}
	defaultGeneratedMarkers = []string{"do not edit", "<auto-generated />"}
)

type options struct {
	// --- booleans (upstream defaults) ---
	byFile, uloc, dryness, cognitive, binary, countUnsupported bool
	noIgnore, noSccIgnore, noGitIgnore, noGitModule, countIgnore bool
	noComplexity, noDuplicates, noCocomo, noSize, noHborder     bool
	minified, generated, minGenerated                           bool
	ignoreMinified, ignoreGenerated, ignoreMinifiedGenerate    bool
	noLarge, includeSymlinks, locomo, costComparison           bool
	sloccountFormat, wide, percent, ci, debug, verbose, trace   bool
	foldAuthors, specialiseCounters                             bool
	couplingWeighted                                            bool

	// --- strings ---
	countAs, cocomoProjectType, sizeUnit, currencySymbol        string
	sqlProject, remapUnknown, remapAll, locomoPreset            string
	locomoConfig, couplingFor, sortBy                           string

	// --- integers ---
	fileGcCount, fileListQueueSize, fileProcessJobWorkers      int
	fileListJobWorkers, fileSummaryJobQueueSize                int
	directoryWalkerJobWorkers, minGenLineLength                int
	historyDepth, historyBuckets                              int
	largeLineCount, largeByteCount                            int64
	averageWage                                               int64

	// --- floats ---
	overhead, eaf, locomoReviewMinutes                         float64
	locomoInputPrice, locomoOutputPrice, locomoTPS             float64
	locomoCycles                                              float64

	// --- slices ---
	excludeDir, allowExt, excludeExt, excludeFile             []string
	notMatch, ignoreFile, generatedMarkers, countAsPattern     []string

	// --- adapter-specific ---
	out         string
	pretty      bool
	reportSkip  string
	reportTitle string
}

// recognisedReportSkip mirrors processor.reportSkipRecognised, the set of
// section names the engine's own --report-skip accepts.
var recognisedReportSkip = map[string]bool{
	"cocomo": true, "locomo": true, "hotspots": true, "coupling": true,
	"authors": true, "timeline": true, "files": true, "uloc": true,
	"linelength": true, "card": true,
}

// applyReportSkip mirrors the unexported processor.parseReportSkip so the
// adapter can honour the same section selection as the CLI's --report.
func applyReportSkip(raw string) {
	processor.ReportSkipNames = map[string]bool{}
	if strings.TrimSpace(raw) == "" {
		return
	}

	for _, part := range strings.Split(raw, ",") {
		name := strings.ToLower(strings.TrimSpace(part))
		if name == "" {
			continue
		}
		if !recognisedReportSkip[name] {
			fmt.Fprintf(os.Stderr,
				"warning: --report-skip: unknown section %q (recognised: cocomo, locomo, hotspots, coupling, authors, timeline, files, uloc, linelength, card)\n",
				name)
		}
		processor.ReportSkipNames[name] = true
	}
}

func (o *options) register(fs *flag.FlagSet) {
	fs.BoolVar(&o.byFile, "by-file", false, "")
	fs.BoolVar(&o.uloc, "uloc", false, "")
	fs.BoolVar(&o.dryness, "dryness", false, "")
	fs.BoolVar(&o.cognitive, "cognitive", false, "")
	fs.BoolVar(&o.binary, "binary", false, "")
	fs.BoolVar(&o.countUnsupported, "count-unsupported", false, "")
	fs.BoolVar(&o.noIgnore, "no-ignore", false, "")
	fs.BoolVar(&o.noSccIgnore, "no-scc-ignore", false, "")
	fs.BoolVar(&o.noGitIgnore, "no-gitignore", false, "")
	fs.BoolVar(&o.noGitModule, "no-gitmodule", false, "")
	fs.BoolVar(&o.countIgnore, "count-ignore", false, "")
	fs.BoolVar(&o.noComplexity, "no-complexity", false, "")
	fs.BoolVar(&o.noDuplicates, "no-duplicates", false, "")
	fs.BoolVar(&o.noCocomo, "no-cocomo", false, "")
	fs.BoolVar(&o.noSize, "no-size", false, "")
	fs.BoolVar(&o.noHborder, "no-hborder", false, "")
	fs.BoolVar(&o.minified, "min", false, "")
	fs.BoolVar(&o.generated, "gen", false, "")
	fs.BoolVar(&o.minGenerated, "min-gen", false, "")
	fs.BoolVar(&o.ignoreMinified, "no-min", false, "")
	fs.BoolVar(&o.ignoreGenerated, "no-gen", false, "")
	fs.BoolVar(&o.ignoreMinifiedGenerate, "no-min-gen", false, "")
	fs.BoolVar(&o.noLarge, "no-large", false, "")
	fs.BoolVar(&o.includeSymlinks, "include-symlinks", false, "")
	fs.BoolVar(&o.locomo, "locomo", false, "")
	fs.BoolVar(&o.costComparison, "cost-comparison", false, "")
	fs.BoolVar(&o.sloccountFormat, "sloccount-format", false, "")
	fs.BoolVar(&o.wide, "wide", false, "")
	fs.BoolVar(&o.percent, "percent", false, "")
	fs.BoolVar(&o.ci, "ci", false, "")
	fs.BoolVar(&o.debug, "debug", false, "")
	fs.BoolVar(&o.verbose, "verbose", false, "")
	fs.BoolVar(&o.trace, "trace", false, "")
	fs.BoolVar(&o.foldAuthors, "no-fold-authors", false, "")
	fs.BoolVar(&o.specialiseCounters, "exp-per-language-counters", false, "")
	fs.BoolVar(&o.couplingWeighted, "coupling-weighted", false, "")

	fs.StringVar(&o.countAs, "count-as", "", "")
	fs.StringVar(&o.cocomoProjectType, "cocomo-project-type", "organic", "")
	fs.StringVar(&o.sizeUnit, "size-unit", "si", "")
	fs.StringVar(&o.currencySymbol, "currency-symbol", "$", "")
	fs.StringVar(&o.sqlProject, "sql-project", "", "")
	fs.StringVar(&o.remapUnknown, "remap-unknown", "", "")
	fs.StringVar(&o.remapAll, "remap-all", "", "")
	fs.StringVar(&o.locomoPreset, "locomo-preset", "medium", "")
	fs.StringVar(&o.locomoConfig, "locomo-config", "", "")
	fs.StringVar(&o.couplingFor, "coupling-for", "", "")
	fs.StringVar(&o.sortBy, "sort", "files", "")

	fs.IntVar(&o.fileGcCount, "file-gc-count", 10000, "")
	fs.IntVar(&o.fileListQueueSize, "file-list-queue-size", runtime.NumCPU(), "")
	fs.IntVar(&o.fileProcessJobWorkers, "file-process-job-workers", runtime.NumCPU()*4, "")
	fs.IntVar(&o.fileListJobWorkers, "file-list-job-workers", runtime.NumCPU(), "")
	fs.IntVar(&o.fileSummaryJobQueueSize, "file-summary-job-queue-size", runtime.NumCPU(), "")
	fs.IntVar(&o.directoryWalkerJobWorkers, "directory-walker-job-workers", 8, "")
	fs.IntVar(&o.minGenLineLength, "min-gen-line-length", 255, "")
	fs.IntVar(&o.historyDepth, "depth", 1000, "")
	fs.IntVar(&o.historyBuckets, "buckets", 60, "")
	fs.Int64Var(&o.largeLineCount, "large-line-count", 40000, "")
	fs.Int64Var(&o.largeByteCount, "large-byte-count", 1000000, "")
	fs.Int64Var(&o.averageWage, "avg-wage", 56286, "")

	fs.Float64Var(&o.overhead, "overhead", 2.4, "")
	fs.Float64Var(&o.eaf, "eaf", 1.0, "")
	fs.Float64Var(&o.locomoReviewMinutes, "locomo-review", 0.01, "")
	fs.Float64Var(&o.locomoInputPrice, "locomo-input-price", 0, "")
	fs.Float64Var(&o.locomoOutputPrice, "locomo-output-price", 0, "")
	fs.Float64Var(&o.locomoTPS, "locomo-tps", 0, "")
	fs.Float64Var(&o.locomoCycles, "locomo-cycles", 0, "")

	fs.Var(newSliceValue(&o.excludeDir), "exclude-dir", "")
	fs.Var(newSliceValue(&o.allowExt), "include-ext", "")
	fs.Var(newSliceValue(&o.excludeExt), "exclude-ext", "")
	fs.Var(newSliceValue(&o.excludeFile), "exclude-file", "")
	fs.Var(newSliceValue(&o.notMatch), "not-match", "")
	fs.Var(newSliceValue(&o.ignoreFile), "ignore-file", "")
	fs.Var(newSliceValue(&o.generatedMarkers), "generated-markers", "")
	fs.Var(newSliceValue(&o.countAsPattern), "count-as-pattern", "")

	fs.StringVar(&o.out, "out", "", "write the JSON document to this file instead of stdout")
	fs.BoolVar(&o.pretty, "pretty", false, "indent the JSON document (diagnostics)")
	fs.StringVar(&o.reportSkip, "report-skip", "",
		"comma separated section names to omit, exactly as scc --report-skip accepts them")
	fs.StringVar(&o.reportTitle, "report-title", "",
		"title shown in the report, exactly as scc --report-title accepts it")
}

// sliceValue implements flag.Value for repeatable string flags.
type sliceValue struct {
	target *[]string
}

func newSliceValue(target *[]string) *sliceValue { return &sliceValue{target: target} }

// String is nil-safe: the flag package calls it on a zero value while
// rendering --help, where the target pointer is nil.
func (s *sliceValue) String() string {
	if s == nil || s.target == nil {
		return ""
	}
	return strings.Join(*s.target, ",")
}

func (s *sliceValue) Set(v string) error {
	if v == "" {
		return nil
	}
	for _, part := range strings.Split(v, ",") {
		part = strings.TrimSpace(part)
		if part != "" {
			*s.target = append(*s.target, part)
		}
	}
	return nil
}

// ---------------------------------------------------------------------------
// Flag application onto the frozen engine
// ---------------------------------------------------------------------------

func mergeSliceDefault(set, defaults []string) []string {
	if len(set) == 0 {
		return append([]string{}, defaults...)
	}
	seen := map[string]struct{}{}
	var out []string
	for _, v := range append(append([]string{}, defaults...), set...) {
		if _, ok := seen[v]; ok {
			continue
		}
		seen[v] = struct{}{}
		out = append(out, v)
	}
	return out
}

func timeString(t time.Time) string {
	if t.IsZero() {
		return ""
	}
	return t.UTC().Format(time.RFC3339)
}

func applyOptions(o *options) {
	// Load the language database exactly once, as scc's Process() does.
	processor.ProcessConstants()

	processor.Files = o.byFile
	processor.UlocMode = o.uloc
	processor.Dryness = o.dryness
	processor.Cognitive = o.cognitive
	processor.DisableCheckBinary = o.binary
	processor.CountUnsupported = o.countUnsupported
	processor.Ignore = o.noIgnore
	processor.SccIgnore = o.noSccIgnore
	processor.GitIgnore = o.noGitIgnore
	processor.GitModuleIgnore = o.noGitModule
	processor.CountIgnore = o.countIgnore
	processor.Complexity = !o.noComplexity
	processor.Duplicates = o.noDuplicates
	processor.Cocomo = o.noCocomo
	processor.Size = o.noSize
	processor.HBorder = o.noHborder
	processor.Minified = o.minified
	processor.Generated = o.generated
	processor.MinifiedGenerated = o.minGenerated
	processor.IgnoreMinified = o.ignoreMinified
	processor.IgnoreGenerated = o.ignoreGenerated
	processor.IgnoreMinifiedGenerate = o.ignoreMinifiedGenerate
	processor.NoLarge = o.noLarge
	processor.IncludeSymLinks = o.includeSymlinks
	processor.Locomo = o.locomo
	processor.CostComparison = o.costComparison
	processor.SLOCCountFormat = o.sloccountFormat
	processor.More = o.wide
	processor.Percent = o.percent
	processor.Ci = o.ci
	processor.Debug = o.debug
	processor.Verbose = o.verbose
	processor.Trace = o.trace
	processor.FoldAuthors = !o.foldAuthors
	processor.SpecialisedCounters = o.specialiseCounters

	processor.CountAs = o.countAs
	processor.CocomoProjectType = o.cocomoProjectType
	processor.SizeUnit = o.sizeUnit
	processor.CurrencySymbol = o.currencySymbol
	processor.SQLProject = o.sqlProject
	processor.RemapUnknown = o.remapUnknown
	processor.RemapAll = o.remapAll
	processor.LocomoPresetName = o.locomoPreset
	processor.LocomoConfig = o.locomoConfig
	processor.CouplingFor = o.couplingFor

	// Accepted for parity with the CLI's flag surface. The engine's report
	// path always orders files by location and ranks raw co-change coupling
	// (processor/report.go resets CouplingWeighted before collecting), so —
	// exactly as with `scc --report` — these do not alter the collected data.
	processor.SortBy = strings.ToLower(o.sortBy)
	processor.CouplingWeighted = o.couplingWeighted

	processor.GcFileCount = o.fileGcCount
	processor.FileListQueueSize = o.fileListQueueSize
	processor.FileProcessJobWorkers = o.fileProcessJobWorkers
	processor.FileListJobWorkers = o.fileListJobWorkers
	processor.FileSummaryJobQueueSize = o.fileSummaryJobQueueSize
	processor.DirectoryWalkerJobWorkers = o.directoryWalkerJobWorkers
	processor.MinifiedGeneratedLineByteLength = o.minGenLineLength
	processor.HistoryDepth = o.historyDepth
	processor.HistoryBuckets = o.historyBuckets
	processor.LargeLineCount = o.largeLineCount
	processor.LargeByteCount = o.largeByteCount
	processor.AverageWage = o.averageWage

	processor.Overhead = o.overhead
	processor.EAF = o.eaf
	processor.LocomoReviewMinutesPerLine = o.locomoReviewMinutes
	processor.LocomoInputPrice = o.locomoInputPrice
	processor.LocomoOutputPrice = o.locomoOutputPrice
	processor.LocomoTPS = o.locomoTPS
	processor.LocomoCyclesOverride = o.locomoCycles

	processor.PathDenyList = mergeSliceDefault(o.excludeDir, defaultExcludeDirs)
	processor.ExcludeFilename = mergeSliceDefault(o.excludeFile, defaultExcludeFiles)
	processor.GeneratedMarkers = mergeSliceDefault(o.generatedMarkers, defaultGeneratedMarkers)
	processor.AllowListExtensions = o.allowExt
	processor.ExcludeListExtensions = o.excludeExt
	processor.Exclude = o.notMatch
	processor.IgnoreFiles = o.ignoreFile
	processor.CountAsPattern = o.countAsPattern

	// Section selection is the same contract the CLI's --report uses.
	applyReportSkip(o.reportSkip)

	// The title the report carries, exactly as the CLI's --report-title does.
	processor.ReportTitle = o.reportTitle

	// CollectReportData only *enables* the per-file detail when the section is
	// not skipped; it relies on the flag default being off. The adapter sets
	// Files from --by-file, so it has to undo that here or a skipped section
	// would still be collected.
	if processor.ReportSkipNames["files"] {
		processor.Files = false
	}

	// These are only meaningful to the CLI report dispatch; force them off so
	// CollectReportData always takes the single-analysis path.
	processor.Hotspots = false
	processor.Coupling = false
	processor.ByAuthor = false
	processor.Timeline = false

	// --- Implications that scc's unexported processFlags() applies ---------
	// Mirrored here one for one so the adapter and the CLI agree on what the
	// flags mean. Source: processor/process.go processFlags().
	if processor.IgnoreMinifiedGenerate {
		processor.MinifiedGenerated = true
		processor.IgnoreMinified = true
		processor.IgnoreGenerated = true
	}
	if processor.MinifiedGenerated {
		processor.Minified = true
		processor.Generated = true
	}
	if processor.IgnoreMinified {
		processor.Minified = true
	}
	if processor.IgnoreGenerated {
		processor.Generated = true
	}
	if processor.Dryness {
		processor.UlocMode = true
	}
	if processor.CostComparison {
		processor.Cocomo = false
		processor.Locomo = true
	}
	if processor.Locomo && !processor.Complexity {
		processor.Complexity = true
	}
	if processor.Cognitive && !processor.Complexity {
		processor.Complexity = true
	}

	// LOCOMO price/throughput/cycles defaults are ambiguous at zero (unset
	// versus explicitly zero), so the engine keeps its own "was it set" bits
	// and only exposes them through main.go. The adapter sets them directly.
	processor.LocomoInputPriceSet = true
	processor.LocomoOutputPriceSet = true
	processor.LocomoTPSSet = true
	processor.LocomoCyclesSet = true
}

// ---------------------------------------------------------------------------
// DTO conversion
// ---------------------------------------------------------------------------

func toWindow(w processor.HistoryWindow) *jsonWindow {
	if w.Commits == 0 && w.Depth == 0 {
		return nil
	}
	return &jsonWindow{Depth: int(w.Depth), Commits: int(w.Commits), From: timeString(w.From), To: timeString(w.To)}
}

func toLanguages(in []processor.LanguageSummary) []jsonLanguage {
	out := make([]jsonLanguage, 0, len(in))
	for _, l := range in {
		out = append(out, jsonLanguage{
			Name:              l.Name,
			Bytes:             l.Bytes,
			CodeBytes:         l.CodeBytes,
			Lines:             l.Lines,
			Code:              l.Code,
			Comment:           l.Comment,
			Blank:             l.Blank,
			Complexity:        l.Complexity,
			Cognitive:         l.Cognitive,
			Count:             l.Count,
			WeightedComplexity: l.WeightedComplexity,
			ULOC:              l.ULOC,
			jsonPercentages: jsonPercentages{
				FilePercent:       l.FilePercent,
				LinePercent:       l.LinePercent,
				CodePercent:       l.CodePercent,
				CommentPercent:    l.CommentPercent,
				BlankPercent:      l.BlankPercent,
				ComplexityPercent: l.ComplexityPercent,
				BytePercent:       l.BytePercent,
			},
		})
	}
	return out
}

func toFiles(in []*processor.FileJob) []jsonFile {
	out := make([]jsonFile, 0, len(in))
	for _, f := range in {
		out = append(out, jsonFile{
			Location:         f.Location,
			Filename:         f.Filename,
			Extension:        f.Extension,
			Language:         f.Language,
			PossibleLanguage: f.PossibleLanguages,
			Bytes:            f.Bytes,
			Lines:            f.Lines,
			Code:             f.Code,
			Comment:          f.Comment,
			Blank:            f.Blank,
			Complexity:       f.Complexity,
			Cognitive:        f.Cognitive,
			Uloc:             f.Uloc,
			Binary:           f.Binary,
			Minified:         f.Minified,
			Generated:        f.Generated,
		})
	}
	return out
}

func toUloc(u *processor.ULOCResult) *jsonUloc {
	if u == nil {
		return nil
	}
	rows := make([]jsonUlocLanguage, 0, len(u.PerLanguage))
	for _, l := range u.PerLanguage {
		rows = append(rows, jsonUlocLanguage{Language: l.Language, ULOC: l.ULOC})
	}
	return &jsonUloc{Global: u.Global, PerLanguage: rows, TotalLines: u.TotalLines, Dryness: u.Dryness}
}

func toLineLength(l *processor.LineLengthResult) *jsonLineLength {
	if l == nil {
		return nil
	}
	buckets := make([]jsonBucket, 0, len(l.Buckets))
	for _, b := range l.Buckets {
		buckets = append(buckets, jsonBucket{Start: b.Start, End: b.End, Label: b.Label, Count: b.Count})
	}
	outliers := make([]jsonOutlier, 0, len(l.Outliers))
	for _, o := range l.Outliers {
		outliers = append(outliers, jsonOutlier{File: o.File, Language: o.Language, LineLength: o.LineLength})
	}
	return &jsonLineLength{Buckets: buckets, Mean: l.Mean, Max: l.Max, Outliers: outliers, TotalLines: l.TotalLines}
}

func toHotspots(h *processor.HotspotsResult) *jsonHotspots {
	if h == nil {
		return nil
	}
	rows := make([]jsonHotspotRow, 0, len(h.Records))
	for _, r := range h.Records {
		rows = append(rows, jsonHotspotRow{
			File:         r.File,
			Language:     r.Language,
			Complexity:   r.Complexity,
			Commits:      r.Commits,
			LinesChanged: r.LinesChanged,
			Authors:      r.Authors,
			CodeChurn:    r.CodeChurn,
			CommentChurn: r.CommentChurn,
			Score:        r.Score,
		})
	}
	return &jsonHotspots{
		Window:    toWindowOrZero(h.Window),
		Records:   rows,
		TotalRaw:  h.TotalRaw,
		Available: h.Available,
	}
}

func toCoupling(c *processor.CouplingResult) *jsonCoupling {
	if c == nil {
		return nil
	}
	pairs := make([]jsonCouplingRow, 0, len(c.Pairs))
	for _, p := range c.Pairs {
		pairs = append(pairs, jsonCouplingRow{FileA: p.FileA, FileB: p.FileB, Shared: p.Shared, Degree: p.Degree})
	}
	return &jsonCoupling{
		Window:     toWindowOrZero(c.Window),
		Pairs:      pairs,
		TotalPairs: c.TotalPairs,
		Available:  c.Available,
	}
}

func toAuthors(a *processor.AuthorsResult) *jsonAuthors {
	if a == nil {
		return nil
	}
	rows := make([]jsonAuthorRow, 0, len(a.Rows))
	for _, r := range a.Rows {
		rows = append(rows, jsonAuthorRow{
			Name:            r.Name,
			Email:           r.Email,
			Code:            r.Code,
			Comment:         r.Comment,
			Complexity:      r.Complexity,
			Files:           r.Files,
			OwnsPercent:     r.OwnsPercent,
			InWindowPercent: r.InWindowPercent,
			LastCommit:      timeString(r.LastCommit),
			Sentinel:        r.Sentinel,
		})
	}
	return &jsonAuthors{
		Window:       toWindowOrZero(a.Window),
		Rows:         rows,
		BusFactor:    a.BusFactor,
		BusAuthors:   append([]string(nil), a.BusAuthors...),
		BusCovered:   a.BusCovered,
		InWindowCode: a.InWindowCode,
	}
}

func toTimeline(t *processor.LangTimelineResult) *jsonLangTimeline {
	if t == nil {
		return nil
	}
	rows := make([]jsonLangTimelineRow, 0, len(t.Rows))
	for _, r := range t.Rows {
		rows = append(rows, jsonLangTimelineRow{
			Language:      r.Language,
			StartingLines: r.StartingLines,
			CodeNow:       r.CodeNow,
			Change:        r.Change,
			SharePercent:  r.SharePercent,
			Deltas:        append([]int64(nil), r.Deltas...),
			Trajectory:    append([]int64(nil), r.Trajectory...),
		})
	}
	return &jsonLangTimeline{Window: toWindowOrZero(t.Window), Buckets: t.Buckets, Rows: rows}
}

func toAuthorTimeline(t *processor.AuthorTimelineResult) *jsonAuthorTimeline {
	if t == nil {
		return nil
	}
	rows := make([]jsonAuthorTimelineRow, 0, len(t.Rows))
	for _, r := range t.Rows {
		rows = append(rows, jsonAuthorTimelineRow{
			Name:         r.Name,
			Email:        r.Email,
			TotalCommits: r.TotalCommits,
			CodeDelta:    r.CodeDelta,
			Series:       toBuckets(r.Series),
		})
	}
	return &jsonAuthorTimeline{Window: toWindowOrZero(t.Window), Buckets: t.Buckets, Rows: rows}
}

func toBuckets(in []processor.AuthorTimelineBucket) []jsonTimelineBucket {
	out := make([]jsonTimelineBucket, 0, len(in))
	for _, b := range in {
		out = append(out, jsonTimelineBucket{Commits: b.Commits, CodeDelta: b.CodeDelta})
	}
	return out
}

func toWindowOrZero(w processor.HistoryWindow) jsonWindow {
	return jsonWindow{Depth: int(w.Depth), Commits: int(w.Commits), From: timeString(w.From), To: timeString(w.To)}
}

func toCocomo(c *processor.CocomoResult) *jsonCocomo {
	if c == nil {
		return nil
	}
	return &jsonCocomo{
		ProjectType:      c.ProjectType,
		SumCode:          c.SumCode,
		EstimatedEffort: c.EstimatedEffort,
		EstimatedCost:   c.EstimatedCost,
		ScheduleMonths:  c.ScheduleMonths,
		PeopleRequired:  c.PeopleRequired,
		AverageWage:      c.AverageWage,
		Overhead:        c.Overhead,
		EAF:              c.EAF,
		CurrencySymbol:   c.CurrencySymbol,
	}
}

func toLocomo(l *processor.LocomoResult) *jsonLocomo {
	if l == nil {
		return nil
	}
	return &jsonLocomo{
		InputTokens:           l.InputTokens,
		OutputTokens:          l.OutputTokens,
		Cost:                  l.Cost,
		GenerationSeconds:     l.GenerationSeconds,
		ReviewHours:           l.ReviewHours,
		AverageComplexityMult: l.AverageComplexityMult,
		IterationFactor:       l.IterationFactor,
		Preset:                l.Preset,
	}
}

// ---------------------------------------------------------------------------
// main
// ---------------------------------------------------------------------------

const usage = `codescope-export — CodeScope's data adapter over the frozen scc backend.

Usage: codescope-export --out report.json [scc analysis flags] <path>

Every analysis flag mirrors the upstream scc flag of the same name, so the GUI
builds one argument list for both engines. Flags that only steer the CLI's own
rendering (--format, --output, --report, --mcp, --config, --languages,
--list-counters) are intentionally absent.

Run codescope-export --help for the full flag list.
`

// windowOrNull finds the first non-empty commit window among the history
// sections, so the GUI can show "last N commits, from → to" metadata.
func windowOrNull(d processor.ReportData) *jsonWindow {
	candidates := []processor.HistoryWindow{}
	if d.Hotspots != nil {
		candidates = append(candidates, d.Hotspots.Window)
	}
	if d.Coupling != nil {
		candidates = append(candidates, d.Coupling.Window)
	}
	if d.Authors != nil {
		candidates = append(candidates, d.Authors.Window)
	}
	if d.LanguageTimeline != nil {
		candidates = append(candidates, d.LanguageTimeline.Window)
	}
	if d.AuthorTimeline != nil {
		candidates = append(candidates, d.AuthorTimeline.Window)
	}
	for _, w := range candidates {
		if w.Commits > 0 || w.Depth > 0 {
			return toWindow(w)
		}
	}
	return nil
}

func main() {
	var showHelp, showVersion bool
	fs := flag.NewFlagSet("codescope-export", flag.ContinueOnError)
	fs.SetOutput(os.Stderr)
	fs.Usage = func() { fmt.Fprint(os.Stderr, usage) }
	fs.BoolVar(&showHelp, "help", false, "show this help")
	fs.BoolVar(&showHelp, "h", false, "show this help")
	fs.BoolVar(&showVersion, "version", false, "print the engine version and exit")

	o := &options{}
	o.register(fs)
	if err := fs.Parse(os.Args[1:]); err != nil {
		if err == flag.ErrHelp {
			os.Exit(0)
		}
		fmt.Fprintln(os.Stderr, "codescope-export: "+err.Error())
		os.Exit(2)
	}
	if showHelp {
		fs.SetOutput(os.Stderr)
		fs.PrintDefaults()
		os.Exit(0)
	}
	if showVersion {
		// Report the same "scc version X" line the engine prints, so the GUI can
		// verify the two bundled executables came from the same build.
		processor.ProcessConstants()
		fmt.Printf("scc version %s\n", processor.Version)
		os.Exit(0)
	}

	rest := fs.Args()
	if len(rest) == 0 {
		fmt.Fprintln(os.Stderr, "codescope-export: no path to analyse; run with --help for usage")
		os.Exit(2)
	}
	path := rest[len(rest)-1]

	applyOptions(o)

	if v := os.Getenv("CODESCOPE_DEBUG"); v != "" {
		fmt.Fprintf(os.Stderr, "debug: GcFileCount=%d Cocomo=%v UlocMode=%v Locomo=%v ReportSkipNames=%v\n",
			processor.GcFileCount, processor.Cocomo, processor.UlocMode, processor.Locomo, processor.ReportSkipNames)
	}

	start := time.Now()
	_ = start
	data, err := processor.CollectReportData(path)
	if err != nil {
		fmt.Fprintln(os.Stderr, "codescope-export: "+err.Error())
		os.Exit(1)
	}

	out := reportOutput{
		SCCVersion:   processor.Version,
		RepoName:     data.RepoName,
		Path:         path,
		GeneratedAt:  data.GeneratedAt.UTC().Format(time.RFC3339),
		DurationMs:   data.Duration.Milliseconds(),
		GitAvailable: data.GitAvailable,
		Window:       windowOrNull(data),
		Totals: jsonTotals{
			Files:      data.Totals.Files,
			Lines:      data.Totals.Lines,
			Code:       data.Totals.Code,
			Comment:    data.Totals.Comment,
			Blank:      data.Totals.Blank,
			Complexity: data.Totals.Complexity,
			Bytes:      data.Totals.Bytes,
		},
		Languages:  toLanguages(data.Summary),
		Files:      toFiles(data.Files),
		ULOC:       toUloc(data.ULOC),
		LineLength: toLineLength(data.LineLength),
		Hotspots:   toHotspots(data.Hotspots),
		Coupling:   toCoupling(data.Coupling),
		Authors:    toAuthors(data.Authors),
		Cocomo:     toCocomo(data.Cocomo),
		Locomo:     toLocomo(data.Locomo),
	}
	tl := &jsonTimeline{Languages: toTimeline(data.LanguageTimeline), Authors: toAuthorTimeline(data.AuthorTimeline)}
	if tl.Languages == nil && tl.Authors == nil {
		// "No history" and "the user turned the section off" are different
		// answers; a nil object is how the GUI tells them apart.
		tl = nil
	}
	out.Timeline = tl

	// CollectReportData always retains the per-file jobs — the engine's own
	// report drops them in the template rather than in the data — so the skip
	// list is applied here to keep the JSON contract identical to the HTML one.
	if processor.ReportSkipNames["files"] {
		out.Files = nil
	}

	var writer io.Writer = os.Stdout
	if o.out != "" {
		f, err := os.Create(o.out)
		if err != nil {
			fmt.Fprintln(os.Stderr, "codescope-export: "+err.Error())
			os.Exit(1)
		}
		defer f.Close()
		writer = f
	}

	enc := json.NewEncoder(writer)
	if o.pretty {
		// Diagnostics only: keep the default compact encoding for the GUI, which
		// parses the document either way but moves far fewer bytes for a
		// 10k-file repository.
		enc.SetIndent("", "  ")
	}
	if err := enc.Encode(out); err != nil {
		fmt.Fprintln(os.Stderr, "codescope-export: "+err.Error())
		os.Exit(1)
	}
}

