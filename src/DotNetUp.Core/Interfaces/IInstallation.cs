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
    /// <returns>Result indicating success or failure</returns>
    /// <exception cref="OperationCanceledException">Thrown when cancellation is requested</exception>
    Task<InstallationResult> ExecuteAsync();
}
