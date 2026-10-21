using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using CodeScope.Models;
using CodeScope.Services;

namespace CodeScope.ViewModels;

/// <summary>
/// Drives the main window: folder selection, report configuration, presets,
/// the run itself and the transition into the report view.
/// </summary>
public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private readonly SettingsStore _settingsStore;
    private readonly BackendService _backend;
    private readonly AnalysisService _analysis;
    private readonly ThemeManager _themeManager;
    private readonly HistoryStore _history;

    private CancellationTokenSource? _runCancellation;

    private string _projectPath = string.Empty;
    private string _statusMessage = "Ready.";
    private string _errorMessage = string.Empty;
    private string _folderSummary = string.Empty;
    private string _folderWarning = string.Empty;
    private string _backendStatus = "Backend: checking…";
    private string _selectedPreset = "Full Report";
    private string _appliedPreset = string.Empty;
    private string _outputFileConfirmation = string.Empty;
    private bool _isRunning;
    private bool _isIndeterminate = true;
    private bool _hasResult;
    private AnalysisResult? _result;
    private FolderInfo _folderInfo = new();
    private string _activeViewKey = "config";
    private object? _activeView;
    private bool _isSidebarExpanded = true;
    private bool _isOnboardingVisible;
    private int _onboardingStep;

    /// <summary>Which view the body area shows.</summary>
    public object? ActiveView
    {
        get => _activeView ?? this;
        set => SetProperty(ref _activeView, value);
    }

    /// <summary>Which view the body area shows.</summary>
    public string ActiveViewKey
    {
        get => _activeViewKey;
        set
        {
            if (SetProperty(ref _activeViewKey, value))
            {
                OnPropertyChanged(nameof(ActiveViewTitle));
                OnPropertyChanged(nameof(ActiveViewSubtitle));
                OnPropertyChanged(nameof(IsReportView));
            }
        }
    }

    /// <summary>True while the report dashboard is on screen.</summary>
    public bool IsReportView => _activeViewKey == "report";

    /// <summary>The title shown in the application bar.</summary>
    public string ActiveViewTitle => IsReportView
        ? (Result?.Data?.RepoName is { Length: > 0 } name ? name : "Report")
        : "New analysis";

    /// <summary>The line under the application bar's title.</summary>
    public string ActiveViewSubtitle
    {
        get
        {
            if (IsReportView)
            {
                var data = Result?.Data;
                if (data == null)
                {
                    return "Report";
                }

                var files = data.Totals.Files.ToString("N0");
                var lines = data.Totals.Lines.ToString("N0");
                return $"{files} files · {lines} lines";
            }

            return string.IsNullOrWhiteSpace(ProjectPath)
                ? "Choose a folder to analyse"
                : ProjectPath;
        }
    }

    /// <summary>Elapsed time of the last run, shown in the status bar.</summary>
    public string ElapsedText
    {
        get => _elapsedText;
        private set => SetProperty(ref _elapsedText, value);
    }

    private string _elapsedText = string.Empty;

    public MainViewModel(
        SettingsStore settingsStore,
        BackendService backend,
        AnalysisService analysis,
        ThemeManager themeManager,
        HistoryStore? historyStore = null)
    {
        _settingsStore = settingsStore;
        _backend = backend;
        _analysis = analysis;
        _themeManager = themeManager;
        _history = historyStore ?? new HistoryStore();

        Configuration = new ReportConfiguration();
        Sections = new ObservableCollection<SectionToggleViewModel>();
        Presets = new ObservableCollection<string>();
        History = new ObservableCollection<HistoryEntryViewModel>();
        OnboardingBullets = new ObservableCollection<string>();

        BrowseCommand = new DelegateCommand(_ => BrowseForFolder());
        GenerateCommand = new DelegateCommand(_ => _ = RunAnalysisAsync(), () => CanRun());
        CancelCommand = new DelegateCommand(_ => CancelRun(), () => IsRunning);
        OpenSettingsCommand = new DelegateCommand(_ => OpenSettings());

        BackToConfigurationCommand = new DelegateCommand(_ => ShowConfiguration());
        SavePresetCommand = new DelegateCommand(_ => SavePreset());
        DeletePresetCommand = new DelegateCommand(_ => DeleteSelectedPreset());
        SelectAllSectionsCommand = new DelegateCommand(_ => SetAllSections(true));
        ClearSectionsCommand = new DelegateCommand(_ => SetAllSections(false));
        RestoreDefaultsCommand = new DelegateCommand(_ => RestoreDefaults());
        SetHtmlModeCommand = new DelegateCommand(_ => SetOutputMode(AnalysisOutputMode.Html));
        SetInAppModeCommand = new DelegateCommand(_ => SetOutputMode(AnalysisOutputMode.InApp));
        SaveReportFileNameCommand = new DelegateCommand(_ => SaveReportFileName());
        BrowseOutputFolderCommand = new DelegateCommand(_ => BrowseOutputFolder());
        OpenProjectFolderCommand = new DelegateCommand(_ => OpenProjectFolder());

        ToggleSidebarCommand = new DelegateCommand(_ => IsSidebarExpanded = !IsSidebarExpanded);
        OpenHistoryEntryCommand = new DelegateCommand(p => OpenHistoryEntry(p as HistoryEntryViewModel));
        DeleteHistoryEntryCommand = new DelegateCommand(p => DeleteHistoryEntry(p as HistoryEntryViewModel));
        ClearHistoryCommand = new DelegateCommand(_ => ClearHistory());
        RevealHistoryFolderCommand = new DelegateCommand(p => RevealHistoryFolder(p as HistoryEntryViewModel));

        OnboardingNextCommand = new DelegateCommand(_ => NextOnboardingStep());
        OnboardingBackCommand = new DelegateCommand(_ => PreviousOnboardingStep());
        OnboardingSkipCommand = new DelegateCommand(_ => FinishOnboarding());
        OnboardingFinishCommand = new DelegateCommand(_ => FinishOnboarding());
        ApplyPresetCommand = new DelegateCommand(p =>
        {
            if (p is string name && name.Length > 0)
            {
                ApplyPreset(name);
            }
        });

        OnboardingStepDots = new ObservableCollection<OnboardingStepDot>();
    }

    // ------------------------------------------------------------------
    // Configuration
    // ------------------------------------------------------------------

    public ReportConfiguration Configuration { get; }

    /// <summary>The preset whose values are currently loaded into the form.</summary>
    public string AppliedPresetName => _appliedPreset;

    public ObservableCollection<SectionToggleViewModel> Sections { get; }

    public ObservableCollection<string> Presets { get; }

    public string SelectedPreset
    {
        get => _selectedPreset;
        set => SetProperty(ref _selectedPreset, value);
    }

    // ------------------------------------------------------------------
    // Project folder
    // ------------------------------------------------------------------

    public string ProjectPath
    {
        get => _projectPath;
        set
        {
            if (SetProperty(ref _projectPath, value))
            {
                RefreshFolderInfo();
                GenerateCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string FolderSummary
    {
        get => _folderSummary;
        private set => SetProperty(ref _folderSummary, value);
    }

    public string FolderWarning
    {
        get => _folderWarning;
        private set => SetProperty(ref _folderWarning, value);
    }

    public FolderInfo FolderInfo => _folderInfo;

    public bool HasGitRepository => _folderInfo.IsGitRepository;

    public string GitStatusText => HasGitRepository
        ? "Git repository detected"
        : "Not a Git repository — history sections will be skipped";

    // ------------------------------------------------------------------
    // Output / status
    // ------------------------------------------------------------------

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                // The banner is bound to this, so it has to be told when the
                // message appears or disappears.
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => _errorMessage.Length > 0;

    public string BackendStatus
    {
        get => _backendStatus;
        private set => SetProperty(ref _backendStatus, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                OnPropertyChanged(nameof(CanGenerate));
                OnPropertyChanged(nameof(CanEditConfiguration));
                GenerateCommand.RaiseCanExecuteChanged();
                CancelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsIndeterminate
    {
        get => _isIndeterminate;
        private set => SetProperty(ref _isIndeterminate, value);
    }

    public bool CanGenerate => CanRun();

    public bool CanEditConfiguration => !IsRunning;

    public string ReportTitleInput
    {
        get => Configuration.ReportTitle;
        set
        {
            if (Configuration.ReportTitle != value)
            {
                Configuration.ReportTitle = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// The report file name as typed in the Output panel. Going through the view
    /// model (rather than binding the model directly) lets the "Save as" action
    /// normalise the name and show confirmation inside the field's row.
    /// </summary>
    public string OutputFileNameInput
    {
        get => Configuration.OutputFileName;
        set
        {
            if (Configuration.OutputFileName != value)
            {
                Configuration.OutputFileName = value;
                OutputFileConfirmation = string.Empty;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>The HTML output folder as typed in the Output panel.</summary>
    public string OutputFolderInput
    {
        get => Configuration.OutputFolder;
        set
        {
            if (Configuration.OutputFolder != value)
            {
                Configuration.OutputFolder = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Inline confirmation shown after "Save as" persists the file name.</summary>
    public string OutputFileConfirmation
    {
        get => _outputFileConfirmation;
        private set
        {
            if (SetProperty(ref _outputFileConfirmation, value))
            {
                OnPropertyChanged(nameof(HasOutputFileConfirmation));
            }
        }
    }

    public bool HasOutputFileConfirmation => _outputFileConfirmation.Length > 0;

    public bool ExportHtml
    {
        get => Configuration.OutputMode == AnalysisOutputMode.Html;
        set => SetOutputMode(value ? AnalysisOutputMode.Html : AnalysisOutputMode.InApp);
    }

    public string GenerateButtonText => Configuration.OutputMode == AnalysisOutputMode.Html
        ? "Generate HTML Report"
        : "Analyze Codebase";

    public bool HasResult
    {
        get => _hasResult;
        private set => SetProperty(ref _hasResult, value);
    }

    public AnalysisResult? Result
    {
        get => _result;
        private set
        {
            if (SetProperty(ref _result, value))
            {
                OnPropertyChanged(nameof(HasResult));
                OnPropertyChanged(nameof(ReportTitle));
            }
        }
    }

    /// <summary>True while the in-app report dashboard should be on screen.</summary>
    public bool ShowReportView
    {
        get => _showReportView;
        private set => SetProperty(ref _showReportView, value);
    }

    /// <summary>The heading shown in the report view.</summary>
    public string ReportTitle =>
        Result?.Data?.RepoName ??
        (!string.IsNullOrWhiteSpace(Configuration.ReportTitle) ? Configuration.ReportTitle : "Report");



    // ------------------------------------------------------------------
    // Commands
    // ------------------------------------------------------------------

    public ICommand BrowseCommand { get; }
    public DelegateCommand GenerateCommand { get; }
    public DelegateCommand CancelCommand { get; }
    public ICommand OpenSettingsCommand { get; }

    public ICommand BackToConfigurationCommand { get; }
    public ICommand SavePresetCommand { get; }
    public ICommand DeletePresetCommand { get; }
    public ICommand SelectAllSectionsCommand { get; }
    public ICommand ClearSectionsCommand { get; }
    public ICommand RestoreDefaultsCommand { get; }
    public ICommand SetHtmlModeCommand { get; }
    public ICommand SetInAppModeCommand { get; }
    public ICommand SaveReportFileNameCommand { get; }
    public ICommand BrowseOutputFolderCommand { get; }
    public ICommand OpenProjectFolderCommand { get; }

    // ------------------------------------------------------------------
    // Sidebar, history and first-run onboarding
    // ------------------------------------------------------------------

    public ICommand ToggleSidebarCommand { get; }
    public ICommand OpenHistoryEntryCommand { get; }
    public ICommand DeleteHistoryEntryCommand { get; }
    public ICommand ClearHistoryCommand { get; }
    public ICommand RevealHistoryFolderCommand { get; }
    public ICommand OnboardingNextCommand { get; }
    public ICommand OnboardingBackCommand { get; }
    public ICommand OnboardingSkipCommand { get; }
    public ICommand OnboardingFinishCommand { get; }
    public ICommand ApplyPresetCommand { get; }

    /// <summary>True when the history sidebar is open.</summary>
    public bool IsSidebarExpanded
    {
        get => _isSidebarExpanded;
        set
        {
            if (SetProperty(ref _isSidebarExpanded, value))
            {
                PersistCurrentSettings();
            }
        }
    }

    /// <summary>Past runs, newest first.</summary>
    public ObservableCollection<HistoryEntryViewModel> History { get; }

    public bool HasHistory => History.Count > 0;

    /// <summary>Shown in the sidebar when nothing has been analysed yet.</summary>
    public string HistoryEmptyText =>
        "Every run is kept here, so you can reopen a report later without analysing the code again.";

    /// <summary>The number of steps in the first-run walkthrough.</summary>
    public const int OnboardingStepCount = 4;

    /// <summary>True while the first-run walkthrough is on screen.</summary>
    public bool IsOnboardingVisible
    {
        get => _isOnboardingVisible;
        private set
        {
            if (SetProperty(ref _isOnboardingVisible, value))
            {
                OnPropertyChanged(nameof(ShowContent));
            }
        }
    }

    /// <summary>True when the normal shell is visible (the walkthrough is over).</summary>
    public bool ShowContent => !IsOnboardingVisible;

    /// <summary>Which step of the walkthrough is showing.</summary>
    public int OnboardingStep
    {
        get => _onboardingStep;
        private set
        {
            if (SetProperty(ref _onboardingStep, value))
            {
                OnPropertyChanged(nameof(IsOnboardingFirstStep));
                OnPropertyChanged(nameof(IsOnboardingLastStep));
                OnPropertyChanged(nameof(IsOnboardingFolderStep));
                OnPropertyChanged(nameof(IsOnboardingContentStep));
                OnPropertyChanged(nameof(IsOnboardingSummaryStep));
                OnPropertyChanged(nameof(OnboardingProgressText));
                OnPropertyChanged(nameof(OnboardingTitle));
                OnPropertyChanged(nameof(OnboardingSubtitle));
                OnPropertyChanged(nameof(OnboardingActionText));
                UpdateOnboardingDots();
            }
        }
    }

    /// <summary>The bullet points of the current walkthrough step.</summary>
    public ObservableCollection<string> OnboardingBullets { get; }

    /// <summary>The dots that show how far through the walkthrough the user is.</summary>
    public ObservableCollection<OnboardingStepDot> OnboardingStepDots { get; }

    /// <summary>True on the step that asks for the folder to analyse.</summary>
    public bool IsOnboardingFolderStep => OnboardingStep == 1;

    /// <summary>True on the step that asks what to include.</summary>
    public bool IsOnboardingContentStep => OnboardingStep == 2;

    /// <summary>True on the closing summary step.</summary>
    public bool IsOnboardingSummaryStep => OnboardingStep == OnboardingStepCount - 1;

    public bool IsOnboardingFirstStep => OnboardingStep == 0;

    public bool IsOnboardingLastStep => OnboardingStep == OnboardingStepCount - 1;

    public string OnboardingProgressText => $"Step {OnboardingStep + 1} of {OnboardingStepCount}";

    public string OnboardingTitle => OnboardingStep switch
    {
        0 => "Welcome to CodeScope",
        1 => "Point it at your code",
        2 => "Choose what to include",
        _ => "You are all set",
    };

    public string OnboardingSubtitle => OnboardingStep switch
    {
        0 => "A native desktop front end for the frozen scc engine — fast counts, honest numbers, no server involved.",
        1 => "CodeScope never writes inside your project. It only reads the folder you pick.",
        2 => "Every section can be turned on or off, and the choice applies to the in-app report and the HTML export alike.",
        _ => "Your choices are remembered, and every run is kept in the sidebar so you can come back to it.",
    };

    public string OnboardingActionText => IsOnboardingLastStep ? "Start analysing" : "Continue";

    private void NextOnboardingStep()
    {
        if (IsOnboardingLastStep)
        {
            FinishOnboarding();
            return;
        }

        OnboardingStep++;
        LoadOnboardingBullets();
    }

    private void PreviousOnboardingStep()
    {
        if (OnboardingStep > 0)
        {
            OnboardingStep--;
            LoadOnboardingBullets();
        }
    }

    private void FinishOnboarding()
    {
        IsOnboardingVisible = false;
        LoadOnboardingBullets();
        PersistCurrentSettings();
    }

    private void LoadOnboardingBullets()
    {
        OnboardingBullets.Clear();
        switch (OnboardingStep)
        {
            case 0:
                OnboardingBullets.Add("Point it at a folder and it counts files, lines, code, comments and complexity.");
                OnboardingBullets.Add("History sections — hotspots, change coupling, authors, timeline — come from the Git log.");
                OnboardingBullets.Add("Read the report in the app, or export the engine's own HTML infographic.");
                break;
            case 1:
                OnboardingBullets.Add("The native folder picker is used — no path typing, no re-implemented tree view.");
                OnboardingBullets.Add("Nothing is written inside the analysed folder, ever.");
                OnboardingBullets.Add("You can change the folder at any time; the walkthrough is only a first look.");
                break;
            case 2:
                OnboardingBullets.Add("Overview and the language breakdown are always present.");
                OnboardingBullets.Add("Turn a section off and it disappears from both output modes, not just on screen.");
                OnboardingBullets.Add("Presets bundle sections, options and output mode into one click.");
                break;
            default:
                OnboardingBullets.Add("Your folder and choices are remembered for the next launch.");
                OnboardingBullets.Add("Every finished run is listed in the sidebar and can be reopened.");
                OnboardingBullets.Add("Settings, presets and the theme live in the sidebar too.");
                break;
        }
    }

    /// <summary>Light up the dot of the current step and dim the ones behind it.</summary>
    private void UpdateOnboardingDots()
    {
        while (OnboardingStepDots.Count < OnboardingStepCount)
        {
            OnboardingStepDots.Add(new OnboardingStepDot());
        }

        for (var i = 0; i < OnboardingStepDots.Count; i++)
        {
            OnboardingStepDots[i].IsActive = i == OnboardingStep;
            OnboardingStepDots[i].IsDone = i < OnboardingStep;
        }
    }

    // ------------------------------------------------------------------
    // Startup
    // ------------------------------------------------------------------

    /// <summary>Restore the last session's preferences and validate the backend.</summary>
    public Task InitializeAsync()
    {
        var settings = _settingsStore.LoadSettings();

        ApplyTheme(settings.Theme);
        _themeManager.Apply(settings.Theme);

        foreach (var name in _settingsStore.ListPresets())
        {
            if (!Presets.Contains(name))
            {
                Presets.Add(name);
            }
        }

        foreach (var builtin in PresetCatalog.BuiltinNames)
        {
            if (!Presets.Contains(builtin))
            {
                Presets.Add(builtin);
            }
        }

        SelectedPreset = settings.LastPresetName is { } last && Presets.Contains(last)
            ? last
            : PresetCatalog.FullReportName;

        ApplyPreset(SelectedPreset, persist: false);

        if (settings.LastProjectFolder is { } folder && Directory.Exists(folder))
        {
            ProjectPath = folder;
        }

        if (!string.IsNullOrEmpty(settings.LastOutputFolder) && Directory.Exists(settings.LastOutputFolder))
        {
            Configuration.OutputFolder = settings.LastOutputFolder;
        }

        if (!string.IsNullOrEmpty(settings.LastReportTitle))
        {
            Configuration.ReportTitle = settings.LastReportTitle;
        }

        if (!string.IsNullOrEmpty(settings.LastReportFileName))
        {
            Configuration.OutputFileName = settings.LastReportFileName;
        }

        RefreshAllProperties();

        RefreshFolderInfo();
        RefreshHistory();
        IsSidebarExpanded = settings.SidebarExpanded;
        LoadOnboardingBullets();
        IsOnboardingVisible = !settings.OnboardingCompleted;
#pragma warning disable CS4014
        _ = ValidateBackendAsync();
#pragma warning restore CS4014
        StatusMessage = "Ready.";
        return Task.CompletedTask;
    }

    private void ApplyTheme(AppTheme theme)
    {
        // ThemeManager is applied here so the Settings page sees the current
        // dictionary immediately; the Settings page only flips the choice.
        _themeManager.Apply(theme);
    }

    private async Task ValidateBackendAsync()
    {
        var problem = await _backend.ValidateAsync(CancellationToken.None);
        BackendStatus = problem == null
            ? $"Backend: scc {_backend.SccVersion} (frozen)"
            : $"Backend problem: {problem}";
    }

    // ------------------------------------------------------------------
    // Folder handling
    // ------------------------------------------------------------------

    /// <summary>
    /// Opens the shell's native folder picker (Windows' own common item dialog,
    /// not a re-implemented tree view).
    /// </summary>
    private void BrowseForFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select the codebase folder to analyse",
            Multiselect = false,
        };

        if (!string.IsNullOrEmpty(ProjectPath) && Directory.Exists(ProjectPath))
        {
            dialog.InitialDirectory = ProjectPath;
        }

        if (dialog.ShowDialog() == true)
        {
            ProjectPath = dialog.FolderName;
        }
    }

    private void RefreshFolderInfo()
    {
        _folderInfo = FolderInspector.Inspect(ProjectPath);
        FolderSummary = _folderInfo.Exists
            ? $"{_folderInfo.DisplayName} — {_folderInfo.FileCount:N0} files, {Format.Bytes(_folderInfo.TotalBytes, "si")}"
            : string.Empty;

        FolderWarning = _folderInfo.Exists
            ? FolderInspector.WarnIfUnusual(ProjectPath, _folderInfo.FileCount, _folderInfo.TotalBytes) ?? string.Empty
            : string.Empty;

        OnPropertyChanged(nameof(FolderInfo));
        OnPropertyChanged(nameof(HasGitRepository));
        OnPropertyChanged(nameof(GitStatusText));

        foreach (var section in Sections)
        {
            section.IsAvailable = !section.RequiresGit || _folderInfo.IsGitRepository;
        }
    }

    private bool CanRun()
    {
        if (IsRunning || !CanEditConfiguration)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(ProjectPath) || !_folderInfo.Exists)
        {
            return false;
        }

        return true;
    }

    // ------------------------------------------------------------------
    // Running
    // ------------------------------------------------------------------

    /// <summary>Switch the body to the report dashboard for a finished run.</summary>
    private void ActivateReportView(AnalysisResult result)
    {
        var reportConfiguration = Configuration.Clone();
        var viewModel = new ReportViewModel(
            reportConfiguration,
            result.Data!,
            ShowConfiguration,
            () => ExportHtmlForResult(result),
            result.Diagnostics);

        ActiveViewKey = "report";
        ActiveView = viewModel;
        ShowReportView = true;
    }

    private async Task ExportHtmlForResult(AnalysisResult result)
    {
        try
        {
            await ExportHtmlForResultCore(result);
        }
        catch (Exception ex)
        {
            ErrorMessage = "The HTML report could not be exported: " + ex.Message;
            StatusMessage = "Export failed.";
        }
    }

    private async Task ExportHtmlForResultCore(AnalysisResult result)
    {
        // The report is written to disk, so ask where it should go first. The
        // folder is optional: an empty one means "beside the analysed code".
        var dialog = new Views.ExportHtmlWindow
        {
            Owner = Application.Current.MainWindow,
        };

        dialog.SetFileNameForDialog(
            string.IsNullOrEmpty(Configuration.OutputFileName)
                ? "scc-report.html"
                : Configuration.OutputFileName);

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        // Re-run in HTML mode: the section selection and analysis options are
        // already part of the active configuration. The export runs on a copy,
        // so the choice made here does not overwrite the settings the user
        // works with. An empty folder means the report is written beside the
        // analysed code.
        var exportConfiguration = Configuration.Clone();
        exportConfiguration.OutputMode = AnalysisOutputMode.Html;
        exportConfiguration.OutputFolder = dialog.ReportFolder;
        exportConfiguration.OutputFileName = dialog.ReportFileName;
        await RunAnalysisAsync(exportConfiguration);
    }

    private async Task RunAnalysisAsync(ReportConfiguration? configuration = null)
    {
        var target = configuration ?? Configuration;

        if (!ConfirmOutputTarget(target))
        {
            return;
        }

        ErrorMessage = string.Empty;
        OutputFileConfirmation = string.Empty;
        HasResult = false;
        Result = null;

        var progress = new Progress<AnalysisStage>(stage =>
        {
            switch (stage)
            {
                case AnalysisStage.Preparing:
                    StatusMessage = "Preparing…";
                    IsIndeterminate = true;
                    break;
                case AnalysisStage.Analysing:
                    StatusMessage = $"Analysing {Path.GetFileName(ProjectPath.TrimEnd(Path.DirectorySeparatorChar))}… the engine reports no per-file progress.";
                    IsIndeterminate = true;
                    break;
                case AnalysisStage.Exporting:
                    StatusMessage = "Writing the HTML report…";
                    IsIndeterminate = true;
                    break;
                case AnalysisStage.Rendering:
                    StatusMessage = "Rendering…";
                    IsIndeterminate = true;
                    break;
                case AnalysisStage.Done:
                    StatusMessage = "Done.";
                    IsIndeterminate = false;
                    break;
                case AnalysisStage.Failed:
                    IsIndeterminate = false;
                    break;
            }
        });

        _runCancellation = new CancellationTokenSource();
        IsRunning = true;
        OnPropertyChanged(nameof(GenerateButtonText));

        try
        {
            var result = await _analysis.RunAsync(target, ProjectPath, progress, _runCancellation.Token);

            Result = result;
            HasResult = true;

            if (result.Success)
            {
                RecordHistory(result);

                StatusMessage = result.Mode == AnalysisOutputMode.Html
                    ? $"Report written to {result.HtmlPath}"
                    : $"Analysis finished in {result.ElapsedMilliseconds:N0} ms.";

                if (result.Data != null && result.Mode == AnalysisOutputMode.InApp)
                {
                    ActivateReportView(result);
                }
                else if (result.Mode == AnalysisOutputMode.Html)
                {
                    ShowReportView = false;
                    ActiveViewKey = "config";
                    ActiveView = this;
                    StatusMessage = $"Report written to {result.HtmlPath}";
                    OpenHtmlReportInBrowser();
                }
            }
            else
            {
                StatusMessage = "Analysis failed.";
                ErrorMessage = result.ErrorMessage;
            }

            RefreshElapsed();
        }
        catch (Exception ex)
        {
            StatusMessage = "Analysis failed.";
            ErrorMessage = $"Unexpected application error: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
            _runCancellation?.Dispose();
            _runCancellation = null;
            OnPropertyChanged(nameof(GenerateButtonText));
            OnPropertyChanged(nameof(CanGenerate));
        }

        // Only a run the user started themselves changes the remembered
        // settings; an export runs on a copy and leaves them alone.
        if (configuration == null)
        {
            PersistCurrentSettings();
        }
    }

    /// <summary>
    /// Resolves the report file and asks before replacing anything. The in-app
    /// mode writes nothing, so it needs no confirmation.
    /// </summary>
    private bool ConfirmOutputTarget(ReportConfiguration configuration)
    {
        configuration.AllowOverwriteReport = false;

        if (configuration.OutputMode != AnalysisOutputMode.Html)
        {
            return true;
        }

        var folder = string.IsNullOrEmpty(configuration.OutputFolder)
            ? ProjectPath
            : configuration.OutputFolder;

        var name = string.IsNullOrEmpty(configuration.OutputFileName)
            ? "scc-report.html"
            : configuration.OutputFileName;

        if (string.IsNullOrEmpty(folder))
        {
            ErrorMessage = "Choose where the HTML report should be written.";
            return false;
        }

        var full = Path.Combine(folder, name);

        if (!File.Exists(full))
        {
            return true;
        }

        var answer = MessageBox.Show(
            $"A report already exists at:\n\n{full}\n\nReplace it?",
            "Replace existing report",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (answer != MessageBoxResult.Yes)
        {
            ErrorMessage = "Nothing was written. Choose a different file name, or confirm replacing the existing report.";
            return false;
        }

        configuration.AllowOverwriteReport = true;
        return true;
    }

    private void CancelRun()
    {
        _runCancellation?.Cancel();
        StatusMessage = "Cancelling…";
    }

    private bool _showReportView;

    // ------------------------------------------------------------------
    // Presets
    // ------------------------------------------------------------------

    public void ApplyPreset(string name, bool persist = true)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        // Re-applying the preset that is already loaded would throw away any
        // edits made since — for example a remembered output folder or title.
        // Selecting a *different* preset is the only thing that loads one.
        if (string.Equals(name, _appliedPreset, StringComparison.Ordinal))
        {
            SelectedPreset = name;
            return;
        }

        ReportConfiguration preset;
        if (PresetCatalog.TryGetBuiltin(name, out var builtin))
        {
            preset = builtin;
        }
        else if (!_settingsStore.LoadPreset(name, out preset))
        {
            return;
        }

        Configuration.Analysis = preset.Analysis.Clone();
        Configuration.Sections = preset.Sections
            .Select(t => new ReportSectionToggle(t.Id, t.Enabled))
            .ToList();
        Configuration.ReportTitle = preset.ReportTitle;
        Configuration.OutputMode = preset.OutputMode;
        Configuration.OutputFolder = preset.OutputFolder;
        Configuration.OutputFileName = preset.OutputFileName;

        _appliedPreset = name;
        SelectedPreset = name;
        OutputFileConfirmation = string.Empty;
        RebuildSectionViewModels();
        RefreshAllProperties();
        ErrorMessage = string.Empty;

        if (persist)
        {
            PersistCurrentSettings();
        }
    }

    private void RebuildSectionViewModels()
    {
        Sections.Clear();
        foreach (var toggle in Configuration.Sections)
        {
            var info = ReportSectionInfo.For(toggle.Id);
            Sections.Add(new SectionToggleViewModel(info, Configuration, toggle.Enabled));
        }

        RefreshFolderInfo();
    }

    private void SetAllSections(bool enabled)
    {
        foreach (var section in Sections)
        {
            section.Enabled = enabled;
        }
    }

    private void RestoreDefaults()
    {
        Configuration.Analysis = new AnalysisOptions();
        Configuration.Sections = ReportConfiguration.CreateDefaultSections();
        Configuration.ReportTitle = string.Empty;
        Configuration.OutputMode = AnalysisOutputMode.InApp;
        Configuration.OutputFolder = string.Empty;
        Configuration.OutputFileName = string.Empty;
        _appliedPreset = PresetCatalog.CustomName;
        SelectedPreset = PresetCatalog.CustomName;
        OutputFileConfirmation = string.Empty;
        RebuildSectionViewModels();
        RefreshAllProperties();
        ErrorMessage = string.Empty;
        StatusMessage = "Defaults restored.";
    }

    private void SetOutputMode(AnalysisOutputMode mode)
    {
        Configuration.OutputMode = mode;
        OnPropertyChanged(nameof(ExportHtml));
        OnPropertyChanged(nameof(GenerateButtonText));
    }

    private void SavePreset()
    {
        var dialog = new Views.PresetNameWindow
        {
            Owner = Application.Current.MainWindow,
            PresetName = SelectedPreset == "Custom" || PresetCatalog.IsBuiltin(SelectedPreset)
                ? string.Empty
                : SelectedPreset,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var name = dialog.PresetName.Trim();
        if (name.Length == 0)
        {
            return;
        }

        _settingsStore.SavePreset(name, Configuration.Clone());

        if (!Presets.Contains(name))
        {
            Presets.Add(name);
        }

        _appliedPreset = name;
        SelectedPreset = name;
        PersistCurrentSettings();
    }

    private void DeleteSelectedPreset()
    {
        var name = SelectedPreset;
        if (string.IsNullOrEmpty(name) || PresetCatalog.IsBuiltin(name))
        {
            MessageBox.Show("Built-in presets cannot be deleted.", "CodeScope",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show($"Delete the preset \"{name}\"?", "CodeScope",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        _settingsStore.DeletePreset(name);
        Presets.Remove(name);
        SelectedPreset = PresetCatalog.FullReportName;
        PersistCurrentSettings();
    }

    // ------------------------------------------------------------------
    // Output helpers
    // ------------------------------------------------------------------

    private void OpenSettings()
    {
        var window = new Views.SettingsWindow
        {
            Owner = Application.Current.MainWindow,
            DataContext = new SettingsViewModel(_settingsStore, _themeManager),
        };

        if (window.ShowDialog() == true)
        {
            PersistCurrentSettings();
        }
    }



    /// <summary>
    /// Persist the typed report file name with an inline confirmation. This is
    /// deliberately not a Save dialog: the user picks the folder separately and
    /// the file itself is written by the analysis run.
    /// </summary>
    private void SaveReportFileName()
    {
        var name = (Configuration.OutputFileName ?? string.Empty).Trim();

        if (name.Length == 0)
        {
            OutputFileConfirmation = string.Empty;
            ErrorMessage = "Type a file name for the report before saving it.";
            return;
        }

        if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            OutputFileConfirmation = string.Empty;
            ErrorMessage = "The report file name contains characters Windows does not allow.";
            return;
        }

        if (Path.GetExtension(name).Length == 0)
        {
            name += ".html";
        }

        Configuration.OutputFileName = name;
        Configuration.AllowOverwriteReport = false;
        OutputFileConfirmation = $"Saved as {name}";
        ErrorMessage = string.Empty;
        StatusMessage = $"Report file name set to {name}.";
        OnPropertyChanged(nameof(OutputFileNameInput));
        PersistCurrentSettings();
    }

    /// <summary>Pick the output folder with the native folder picker.</summary>
    private void BrowseOutputFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Choose the folder for the HTML report",
            Multiselect = false,
        };

        var current = Configuration.OutputFolder;
        if (string.IsNullOrEmpty(current) || !Directory.Exists(current))
        {
            current = Directory.Exists(ProjectPath) ? ProjectPath : string.Empty;
        }

        if (!string.IsNullOrEmpty(current))
        {
            dialog.InitialDirectory = current;
        }

        if (dialog.ShowDialog() == true)
        {
            OutputFolderInput = dialog.FolderName;
            PersistCurrentSettings();
        }
    }

    private void OpenProjectFolder()
    {
        if (!string.IsNullOrEmpty(ProjectPath) && Directory.Exists(ProjectPath))
        {
            Services.BackendService.OpenInFileExplorer(ProjectPath);
        }
    }

    private void OpenHtmlReportInBrowser()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = Result?.HtmlPath,
                UseShellExecute = true,
            });
        }
        catch
        {
            // Opening the browser is a convenience action.
        }
    }

    private void ShowConfiguration()
    {
        ShowReportView = false;
        ActiveViewKey = "config";
        ActiveView = this;
        StatusMessage = "Ready.";
    }

    /// <summary>Refresh the elapsed-time readout after a run completes.</summary>
    public void RefreshElapsed()
    {
        if (Result is not { Success: true } result)
        {
            ElapsedText = string.Empty;
            return;
        }

        var seconds = result.ElapsedMilliseconds / 1000d;
        ElapsedText = seconds < 10
            ? $"{seconds:0.00} s"
            : seconds < 60
                ? $"{seconds:0.0} s"
                : $"{seconds / 60:0.0} min";
    }

    // ------------------------------------------------------------------
    // History
    // ------------------------------------------------------------------

    /// <summary>Reload the stored runs into the sidebar.</summary>
    public void RefreshHistory()
    {
        History.Clear();
        foreach (var entry in _history.Load())
        {
            History.Add(new HistoryEntryViewModel(entry));
        }

        OnPropertyChanged(nameof(HasHistory));
    }

    /// <summary>Reopen a stored run in the dashboard, exactly as it was produced.</summary>
    private void OpenHistoryEntry(HistoryEntryViewModel? item)
    {
        if (item is null || item.Entry?.Data is not { } data)
        {
            return;
        }

        var configuration = Configuration.Clone();

        // The run was produced with its own section selection; the reopened
        // report shows what that run actually contained.
        foreach (var info in ReportSectionInfo.All)
        {
            if (info.IsCore)
            {
                continue;
            }

            var wasOff = item.Entry.DisabledSections.Contains(info.Id.ToString());
            configuration.SetEnabled(info.Id, !wasOff);
        }

        var viewModel = new ReportViewModel(
            configuration,
            data,
            ShowConfiguration,
            () => ExportHistoryEntry(item));

        ActiveViewKey = "report";
        ActiveView = viewModel;
        ShowReportView = true;
        StatusMessage = $"Showing the run from {item.WhenText}.";
    }

    /// <summary>Re-run a stored run's project and write it out as an HTML report.</summary>
    private async Task ExportHistoryEntry(HistoryEntryViewModel item)
    {
        if (!string.IsNullOrEmpty(item.ProjectPath) && Directory.Exists(item.ProjectPath))
        {
            ProjectPath = item.ProjectPath;
        }

        Configuration.OutputMode = AnalysisOutputMode.Html;
        OnPropertyChanged(nameof(ExportHtml));
        await RunAnalysisAsync();
    }

    private void DeleteHistoryEntry(HistoryEntryViewModel? item)
    {
        if (item == null)
        {
            return;
        }

        _history.Delete(item.Id);
        RefreshHistory();
        StatusMessage = $"Removed the run from {item.WhenText}.";
    }

    private void ClearHistory()
    {
        if (!History.Any())
        {
            return;
        }

        _history.Clear();
        RefreshHistory();
        StatusMessage = "History cleared.";
    }

    private void RevealHistoryFolder(HistoryEntryViewModel? item)
    {
        if (item == null || string.IsNullOrEmpty(item.ProjectPath))
        {
            return;
        }

        if (Directory.Exists(item.ProjectPath))
        {
            Services.BackendService.OpenInFileExplorer(item.ProjectPath);
        }
        else if (!string.IsNullOrEmpty(item.HtmlPath) && File.Exists(item.HtmlPath))
        {
            Services.BackendService.OpenInFileExplorer(item.HtmlPath);
        }
    }

    /// <summary>Store a finished run so it can be reopened later.</summary>
    private void RecordHistory(AnalysisResult result)
    {
        var data = result.Data;
        var trimmed = result.ProjectPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = trimmed.Length > 0 ? Path.GetFileName(trimmed) : string.Empty;

        var entry = new HistoryEntry
        {
            Id = HistoryStore.NewId(DateTimeOffset.Now),
            Timestamp = DateTimeOffset.Now,
            ProjectPath = result.ProjectPath,
            ProjectName = name,
            Mode = result.Mode,
            PresetName = AppliedPresetName,
            ReportTitle = Configuration.ReportTitle,
            Files = data?.Totals.Files ?? 0,
            Lines = data?.Totals.Lines ?? 0,
            Code = data?.Totals.Code ?? 0,
            Complexity = data?.Totals.Complexity ?? 0,
            DurationMs = result.ElapsedMilliseconds,
            HtmlPath = result.HtmlPath,
            DisabledSections = DisabledSectionNames(),
            Data = data,
        };

        _history.Save(entry);
        RefreshHistory();
    }

    /// <summary>The optional sections that were turned off for this run.</summary>
    private List<string> DisabledSectionNames()
    {
        var disabled = new List<string>();
        foreach (var info in ReportSectionInfo.All)
        {
            if (info.IsCore)
            {
                continue;
            }

            if (!Configuration.IsEnabled(info.Id))
            {
                disabled.Add(info.Id.ToString());
            }
        }

        return disabled;
    }

    // ------------------------------------------------------------------
    // Persistence
    // ------------------------------------------------------------------

    private void PersistCurrentSettings()
    {
        try
        {
            var settings = _settingsStore.LoadSettings();
            settings.Theme = _themeManager.RequestedTheme;
            settings.LastProjectFolder = Directory.Exists(ProjectPath) ? ProjectPath : null;
            settings.LastOutputFolder =
                !string.IsNullOrEmpty(Configuration.OutputFolder) && Directory.Exists(Configuration.OutputFolder)
                    ? Configuration.OutputFolder
                    : null;
            settings.LastReportTitle =
                string.IsNullOrEmpty(Configuration.ReportTitle) ? null : Configuration.ReportTitle;
            settings.LastReportFileName =
                string.IsNullOrEmpty(Configuration.OutputFileName) ? null : Configuration.OutputFileName;
            settings.LastPresetName = SelectedPreset;
            settings.CustomPresets = Presets.Where(p => !PresetCatalog.IsBuiltin(p)).ToList();
            settings.SidebarExpanded = IsSidebarExpanded;
            settings.OnboardingCompleted = settings.OnboardingCompleted || !IsOnboardingVisible;
            _settingsStore.SaveSettings(settings);
        }
        catch
        {
            // Persisting is best-effort.
        }
    }

    public void Dispose()
    {
        _runCancellation?.Cancel();
        _runCancellation?.Dispose();
        _themeManager.Dispose();
    }
}
