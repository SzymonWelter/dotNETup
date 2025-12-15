namespace DotNetUp.Core.Models;

/// <summary>
/// Represents the current progress of an installation.
/// Combines executor-level tracking (step number) with step-level details (substep, percentage).
/// </summary>
public class InstallationProgress
{
    /// <summary>
    /// Current step number (1-based). Set by executor.
    /// </summary>
    public int CurrentStepNumber { get; init; }

    /// <summary>
    /// Total number of steps in the installation. Set by executor.
    /// </summary>
    public int TotalSteps { get; init; }

    /// <summary>
    /// Name of the current step (from IInstallationStep.Name). Set by executor.
    /// </summary>
    public string CurrentStepName { get; init; } = string.Empty;

    /// <summary>
    /// Description of the current substep within the step. Set by the step itself.
    /// Example: "Backing up existing file", "Copying file", "Setting permissions"
    /// </summary>
    public string? SubStepDescription { get; init; }

    /// <summary>
    /// Completion percentage of the current step (0-100). Set by the step itself.
    /// </summary>
    public int PercentComplete { get; init; }

    /// <summary>
    /// Calculates overall installation progress across all steps.
    /// Formula: ((completed_steps * 100) + current_step_percent) / total_steps
    /// </summary>
    public double OverallPercentComplete =>
        TotalSteps > 0
            ? ((CurrentStepNumber - 1) * 100.0 + PercentComplete) / TotalSteps
            : 0;
}
