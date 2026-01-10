using DotNetUp.Core.Interfaces;
using DotNetUp.Core.Models;

namespace DotNetUp.Tests.Fixtures;

/// <summary>
/// Mock installation step for testing. Configurable to simulate success/failure.
/// </summary>
public class MockInstallationStep : IInstallationStep
{
    public string Name { get; set; } = "MockStep";
    public string Description { get; set; } = "Mock step for testing";

    public bool ValidateShouldSucceed { get; set; } = true;
    public bool ExecuteShouldSucceed { get; set; } = true;
    public bool RollbackShouldSucceed { get; set; } = true;

    public TimeSpan ExecutionDelay { get; set; } = TimeSpan.Zero;
    public List<string> CallHistory { get; } = new();

    /// <summary>
    /// Optional callback to override Execute behavior. If set, this is called instead of the default logic.
    /// </summary>
    public Func<InstallationStepResult>? ExecuteCallback { get; set; }

    public bool ValidateCalled { get; private set; }
    public bool ExecuteCalled { get; private set; }
    public bool RollbackCalled { get; private set; }

    public Task<InstallationStepResult> ValidateAsync(InstallationContext context)
    {
        ValidateCalled = true;
        CallHistory.Add("Validate");

        return Task.FromResult(ValidateShouldSucceed
            ? InstallationStepResult.SuccessResult($"{Name} validation succeeded")
            : InstallationStepResult.FailureResult($"{Name} validation failed"));
    }

    public async Task<InstallationStepResult> ExecuteAsync(InstallationContext context)
    {
        ExecuteCalled = true;
        CallHistory.Add("Execute");

        if (ExecutionDelay > TimeSpan.Zero)
            await Task.Delay(ExecutionDelay, context.CancellationToken);

        // If a callback is provided, use it instead of the default logic
        if (ExecuteCallback != null)
            return ExecuteCallback();

        return ExecuteShouldSucceed
            ? InstallationStepResult.SuccessResult($"{Name} executed")
            : InstallationStepResult.FailureResult($"{Name} execution failed");
    }

    public Task<InstallationStepResult> RollbackAsync(InstallationContext context)
    {
        RollbackCalled = true;
        CallHistory.Add("Rollback");

        return Task.FromResult(RollbackShouldSucceed
            ? InstallationStepResult.SuccessResult($"{Name} rolled back")
            : InstallationStepResult.FailureResult($"{Name} rollback failed"));
    }

    public bool DisposeCalled { get; private set; }

    public ValueTask DisposeAsync()
    {
        DisposeCalled = true;
        CallHistory.Add("Dispose");
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
