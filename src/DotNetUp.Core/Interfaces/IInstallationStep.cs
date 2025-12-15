using DotNetUp.Core.Models;

namespace DotNetUp.Core.Interfaces;

/// <summary>
/// Defines the contract for an installation step.
/// Every installation operation (copy files, create registry keys, install services, etc.)
/// must implement this interface.
/// </summary>
public interface IInstallationStep
{
    /// <summary>
    /// Gets the unique name of this installation step.
    /// Used for logging and identifying steps during execution.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the description of what this step does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Validates that the step can be executed.
    /// Checks prerequisites, permissions, and required resources.
    /// Does not modify the system.
    /// </summary>
    /// <param name="context">The installation context</param>
    /// <returns>A result indicating whether validation succeeded</returns>
    Task<InstallationResult> ValidateAsync(InstallationContext context);

    /// <summary>
    /// Executes the installation step.
    /// Performs the actual work (copy files, create registry keys, etc.).
    /// Should only be called after successful validation.
    /// </summary>
    /// <param name="context">The installation context</param>
    /// <returns>A result indicating whether execution succeeded</returns>
    Task<InstallationResult> ExecuteAsync(InstallationContext context);

    /// <summary>
    /// Rolls back the changes made by this step.
    /// Called if a subsequent step fails, to undo changes and restore the system.
    /// This is a best-effort operation - failures during rollback are logged but do not stop the rollback process.
    /// </summary>
    /// <param name="context">The installation context</param>
    /// <returns>A result indicating the rollback outcome (failures are logged but not critical)</returns>
    Task<InstallationResult> RollbackAsync(InstallationContext context);
}
