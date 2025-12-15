using Microsoft.Extensions.Logging;

namespace DotNetUp.Core.Models;

/// <summary>
/// Provides shared state and services for installation steps.
/// This context is passed to all steps during validation, execution, and rollback.
/// </summary>
public class InstallationContext
{
    /// <summary>
    /// A dictionary for sharing data between installation steps.
    /// Steps can write to and read from this to pass information.
    /// </summary>
    public Dictionary<string, object> Properties { get; } = new();

    /// <summary>
    /// Logger for recording installation activities.
    /// </summary>
    public ILogger Logger { get; }

    /// <summary>
    /// Progress reporter for tracking installation progress.
    /// Can be used to update UI or report status to monitoring systems.
    /// </summary>
    public IProgress<InstallationProgress>? Progress { get; init; }

    /// <summary>
    /// Cancellation token for cancelling the installation process.
    /// Steps should check this token and cancel gracefully if requested.
    /// </summary>
    public CancellationToken CancellationToken { get; init; }

    // Internal state for progress tracking
    private int _currentStepNumber;
    private int _totalSteps;
    private string _currentStepName = string.Empty;

    /// <summary>
    /// Creates a new installation context.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="progress">Optional progress reporter</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    public InstallationContext(
        ILogger logger,
        IProgress<InstallationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Progress = progress;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Sets the current step context. Called by the executor before executing each step.
    /// </summary>
    /// <param name="stepNumber">Current step number (1-based)</param>
    /// <param name="totalSteps">Total number of steps</param>
    /// <param name="stepName">Name of the current step</param>
    internal void SetCurrentStep(int stepNumber, int totalSteps, string stepName)
    {
        _currentStepNumber = stepNumber;
        _totalSteps = totalSteps;
        _currentStepName = stepName;
    }

    /// <summary>
    /// Reports progress for the current step. Called by the step implementation.
    /// </summary>
    /// <param name="subStepDescription">Description of the current substep</param>
    /// <param name="percentComplete">Completion percentage (0-100)</param>
    public void ReportStepProgress(string subStepDescription, int percentComplete)
    {
        if (percentComplete < 0 || percentComplete > 100)
            throw new ArgumentOutOfRangeException(nameof(percentComplete),
                "Percent complete must be between 0 and 100");

        var progress = new InstallationProgress
        {
            CurrentStepNumber = _currentStepNumber,
            TotalSteps = _totalSteps,
            CurrentStepName = _currentStepName,
            SubStepDescription = subStepDescription,
            PercentComplete = percentComplete
        };

        Progress?.Report(progress);
        Logger.LogInformation(
            "Step {StepNumber}/{TotalSteps} ({StepName}): {SubStep} - {Percent}% ({Overall:F1}% overall)",
            progress.CurrentStepNumber,
            progress.TotalSteps,
            progress.CurrentStepName,
            subStepDescription,
            percentComplete,
            progress.OverallPercentComplete);
    }
}
