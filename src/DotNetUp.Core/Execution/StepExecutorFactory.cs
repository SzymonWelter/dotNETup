using Microsoft.Extensions.Logging;

namespace DotNetUp.Core.Execution;

/// <summary>
/// Factory that builds the step executor chain with all decorators.
/// The chain is: Retry -> Timeout -> Base Executor
/// </summary>
internal static class StepExecutorFactory
{
    /// <summary>
    /// Creates the step executor chain with all necessary decorators.
    /// </summary>
    /// <param name="logger">Logger for decorators that need logging</param>
    /// <returns>The outermost executor in the chain</returns>
    public static IStepExecutor Create(ILogger logger)
    {
        // Build from inside out:
        // Base executor (actually calls the step)
        IStepExecutor executor = new StepExecutor();

        // Timeout decorator (applies per-step timeout)
        executor = new TimeoutStepExecutor(executor);

        // Retry decorator (retries on failure) - outermost so retries include timeout
        executor = new RetryStepExecutor(executor, logger);

        return executor;
    }
}
