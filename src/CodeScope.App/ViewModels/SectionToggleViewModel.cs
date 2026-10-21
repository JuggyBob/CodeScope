using CodeScope.Models;

namespace CodeScope.ViewModels;

/// <summary>One toggleable report section, bound from the Report Contents panel.</summary>
public sealed class SectionToggleViewModel : ViewModelBase
{
    private readonly ReportConfiguration _configuration;
    private bool _enabled;
    private bool _isAvailable = true;

    public SectionToggleViewModel(ReportSectionInfo info, ReportConfiguration configuration, bool enabled)
    {
        Info = info;
        _configuration = configuration;
        _enabled = enabled;
    }

    public ReportSectionInfo Info { get; }

    public ReportSectionId Id => Info.Id;

    public string Name => Info.Name;

    public string Description => Info.Description;

    public bool IsCore => Info.IsCore;

    public bool RequiresGit => Info.RequiresGit;

    /// <summary>True when the folder under analysis can produce this section.</summary>
    public bool IsAvailable
    {
        get => _isAvailable;
        set
        {
            if (SetProperty(ref _isAvailable, value))
            {
                OnPropertyChanged(nameof(AvailabilityText));
                OnPropertyChanged(nameof(AvailabilityBadge));
            }
        }
    }

    /// <summary>Short badge shown next to the name when the section needs Git.</summary>
    public string AvailabilityBadge => RequiresGit
        ? (IsAvailable ? "Git ✓" : "Git — unavailable")
        : string.Empty;

    public string? SccSkipName => Info.SccSkipName;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (SetProperty(ref _enabled, value))
            {
                _configuration.SetEnabled(Id, value);
            }
        }
    }

    /// <summary>The explanation shown when this section cannot be produced.</summary>
    public string AvailabilityText => IsAvailable
        ? string.Empty
        : "This folder is not inside a Git repository, so this section cannot be produced.";
}
