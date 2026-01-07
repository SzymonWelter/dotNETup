using DotNetUp.Core.Models;

namespace DotNetUp.Core.Interfaces;

/// <summary>
/// Represents an installation that can be executed.
/// Orchestrates the execution of installation steps with validation, execution, and rollback.
/// </summary>
public interface IInstallation
{
    /// <summary>
    /// Executes the installation by validating all steps, executing them in sequence,
    /// and rolling back if any step fails.
    /// </summary>
    /// <returns>Summary of the installation outcome</returns>
    /// <exception cref="OperationCanceledException">Thrown when cancellation is requested</exception>
    Task<InstallationSummary> InstallAsync();

    /// <summary>
    /// Executes uninstallation by running steps in reverse order with uninstall mode enabled.
    /// </summary>
    /// <returns>Summary of the uninstallation outcome</returns>
    /// <exception cref="OperationCanceledException">Thrown when cancellation is requested</exception>
    Task<InstallationSummary> UninstallAsync();

    /// <summary>
    /// Re-executes specific steps for repair purposes.
    /// </summary>
    /// <param name="stepNames">Names of steps to re-execute. If null or empty, all steps are re-executed.</param>
    /// <returns>Summary of the repair outcome</returns>
    /// <exception cref="OperationCanceledException">Thrown when cancellation is requested</exception>
    Task<InstallationSummary> RepairAsync(params string[] stepNames);
}
