namespace DotNetUp.Core.Models;

/// <summary>
/// Configuration options for an individual installation step.
/// Allows customizing step behavior without modifying the step implementation.
/// </summary>
public class InstallationStepOptions
{
    /// <summary>
    /// Override the step's default name.
    /// Allows using the same step type multiple times with different names.
    /// Default: null (uses the step's built-in Name property).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Override the step's default description.
    /// Used for progress reporting and logging.
    /// Default: null (uses the step's built-in Description property).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Continue installation if this step fails.
    /// When true, failure is logged but installation proceeds to the next step.
    /// When false, failure triggers rollback (if enabled in InstallationOptions).
    /// Default: false (step failure aborts installation).
    /// </summary>
    public bool ContinueOnError { get; set; } = false;

    /// <summary>
    /// Skip this step based on a runtime condition.
    /// The predicate is evaluated before the step's ValidateAsync.
    /// If the predicate returns true, the step is skipped entirely.
    /// Example: Skip "Install Runtime" if already installed.
    /// Default: null (step always runs).
    /// </summary>
    public Func<InstallationContext, Task<bool>>? ShouldSkip { get; set; }

    /// <summary>
    /// Per-step timeout, overriding the installation-level timeout.
    /// Null means use the installation's global timeout.
    /// Default: null.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Number of times to retry this step if it fails.
    /// Retries only apply when ContinueOnError is false.
    /// Default: 0 (no retries).
    /// </summary>
    public int RetryCount { get; set; } = 0;
}
