using DotNetUp.Core.Models;

namespace DotNetUp.Core.Execution;

/// <summary>
/// Defines the contract for executing an installation step.
/// Implementations can decorate the execution with additional behavior
/// such as retries, timeouts, or error handling.
/// </summary>
internal interface IStepExecutor
{
    /// <summary>
    /// Executes a step and returns the result.
    /// </summary>
    /// <param name="configuredStep">The configured step to execute</param>
    /// <param name="context">Installation context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The result of the step execution</returns>
    Task<InstallationStepResult> ExecuteAsync(
        ConfiguredStep configuredStep,
        InstallationContext context,
        CancellationToken cancellationToken);
}
