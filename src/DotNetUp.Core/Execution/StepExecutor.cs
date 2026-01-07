using DotNetUp.Core.Models;

namespace DotNetUp.Core.Execution;

/// <summary>
/// Base step executor that simply calls ExecuteAsync on the step.
/// This is the innermost executor in the decorator chain.
/// </summary>
internal class StepExecutor : IStepExecutor
{
    public Task<InstallationStepResult> ExecuteAsync(
        ConfiguredStep configuredStep,
        InstallationContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return configuredStep.Step.ExecuteAsync(context);
    }
}
