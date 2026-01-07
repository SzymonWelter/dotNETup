using DotNetUp.Core.Models;

namespace DotNetUp.Core.Execution;

/// <summary>
/// Decorator that adds per-step timeout to step execution.
/// Wraps another executor and applies a timeout if configured in step options.
/// </summary>
internal class TimeoutStepExecutor : IStepExecutor
{
    private readonly IStepExecutor _inner;

    public TimeoutStepExecutor(IStepExecutor inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public async Task<InstallationStepResult> ExecuteAsync(
        ConfiguredStep configuredStep,
        InstallationContext context,
        CancellationToken cancellationToken)
    {
        var stepTimeout = configuredStep.Options.Timeout;

        // If no timeout configured, just pass through
        if (!stepTimeout.HasValue)
        {
            return await _inner.ExecuteAsync(configuredStep, context, cancellationToken);
        }

        using var stepTimeoutCts = new CancellationTokenSource(stepTimeout.Value);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, stepTimeoutCts.Token);

        try
        {
            return await _inner.ExecuteAsync(configuredStep, context, linkedCts.Token);
        }
        catch (OperationCanceledException) when (stepTimeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return InstallationStepResult.FailureResult(
                $"Step timed out after {stepTimeout.Value}");
        }
    }
}
