using DotNetUp.Core.Models;
using Microsoft.Extensions.Logging;

namespace DotNetUp.Core.Execution;

/// <summary>
/// Decorator that adds retry logic to step execution.
/// Wraps another executor and retries on failure according to step options.
/// </summary>
internal class RetryStepExecutor : IStepExecutor
{
    private readonly IStepExecutor _inner;
    private readonly ILogger _logger;

    public RetryStepExecutor(IStepExecutor inner, ILogger logger)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<InstallationStepResult> ExecuteAsync(
        ConfiguredStep configuredStep,
        InstallationContext context,
        CancellationToken cancellationToken)
    {
        var retryCount = configuredStep.Options.RetryCount;

        // If no retries configured, just pass through
        if (retryCount <= 0)
        {
            return await _inner.ExecuteAsync(configuredStep, context, cancellationToken);
        }

        for (int attempt = 0; attempt <= retryCount; attempt++)
        {
            if (attempt > 0)
            {
                _logger.LogInformation(
                    "Retrying step {StepName} (attempt {Attempt}/{MaxAttempts})",
                    configuredStep.Name, attempt + 1, retryCount + 1);
            }

            try
            {
                var result = await _inner.ExecuteAsync(configuredStep, context, cancellationToken);

                // If successful or this is the last attempt, return the result
                if (result.Success || attempt >= retryCount)
                {
                    return result;
                }

                // Log retry attempt
                _logger.LogWarning(
                    "Step {StepName} failed on attempt {Attempt}: {Message}",
                    configuredStep.Name, attempt + 1, result.Message);
            }
            catch (OperationCanceledException)
            {
                // Don't retry on cancellation
                throw;
            }
            catch (Exception ex)
            {
                if (attempt >= retryCount)
                {
                    return InstallationStepResult.FailureResult(
                        $"Step failed after {retryCount + 1} attempts: {ex.Message}",
                        ex);
                }

                _logger.LogWarning(
                    ex,
                    "Step {StepName} threw exception on attempt {Attempt}",
                    configuredStep.Name, attempt + 1);
            }
        }

        // Should not reach here, but return failure just in case
        return InstallationStepResult.FailureResult("Step failed after all retry attempts");
    }
}
