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
    public IProgress<string>? Progress { get; init; }

    /// <summary>
    /// Cancellation token for cancelling the installation process.
    /// Steps should check this token and cancel gracefully if requested.
    /// </summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Creates a new installation context.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="progress">Optional progress reporter</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    public InstallationContext(
        ILogger logger,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Progress = progress;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Reports progress if a progress reporter is configured.
    /// </summary>
    /// <param name="message">Progress message</param>
    public void ReportProgress(string message)
    {
        Progress?.Report(message);
        Logger.LogInformation("Progress: {Message}", message);
    }
}
