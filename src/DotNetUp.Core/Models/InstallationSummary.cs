namespace DotNetUp.Core.Models;

/// <summary>
/// Summary of installation execution.
/// Provides a complete picture of the installation outcome for post-installation analysis.
/// </summary>
public class InstallationSummary
{
    /// <summary>
    /// Overall success indicator.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Summary message describing the outcome.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Exception if the installation failed.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Dictionary of results for each step, keyed by step name.
    /// </summary>
    public Dictionary<string, InstallationStepResult> StepResults { get; init; } = new();

    /// <summary>
    /// Total time taken for the installation.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Number of steps that completed successfully.
    /// </summary>
    public int CompletedSteps { get; init; }

    /// <summary>
    /// Name of the step that failed, if any.
    /// </summary>
    public string? FailedStep { get; init; }

    /// <summary>
    /// Creates a successful installation summary.
    /// </summary>
    /// <param name="message">Success message</param>
    /// <param name="stepResults">Results of each step</param>
    /// <param name="duration">Total duration</param>
    /// <returns>A successful InstallationSummary</returns>
    public static InstallationSummary SuccessSummary(
        string message,
        Dictionary<string, InstallationStepResult> stepResults,
        TimeSpan duration)
    {
        return new InstallationSummary
        {
            Success = true,
            Message = message,
            StepResults = stepResults,
            Duration = duration,
            CompletedSteps = stepResults.Count
        };
    }

    /// <summary>
    /// Creates a failed installation summary.
    /// </summary>
    /// <param name="message">Failure message</param>
    /// <param name="failedStep">Name of the step that failed</param>
    /// <param name="stepResults">Results of each step (including the failed one)</param>
    /// <param name="duration">Total duration</param>
    /// <param name="exception">Exception that caused the failure</param>
    /// <returns>A failed InstallationSummary</returns>
    public static InstallationSummary FailureSummary(
        string message,
        string failedStep,
        Dictionary<string, InstallationStepResult> stepResults,
        TimeSpan duration,
        Exception? exception = null)
    {
        return new InstallationSummary
        {
            Success = false,
            Message = message,
            Exception = exception,
            StepResults = stepResults,
            Duration = duration,
            CompletedSteps = stepResults.Count(r => r.Value.Success),
            FailedStep = failedStep
        };
    }
}
