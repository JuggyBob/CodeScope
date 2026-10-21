namespace CodeScope.ViewModels;

/// <summary>
/// One dot in the walkthrough's progress indicator. The view model keeps
/// four of them in step with the current step.
/// </summary>
public sealed class OnboardingStepDot
{
    /// <summary>True for the step the user is looking at.</summary>
    public bool IsActive { get; set; }

    /// <summary>True for the steps already behind the user.</summary>
    public bool IsDone { get; set; }
}
