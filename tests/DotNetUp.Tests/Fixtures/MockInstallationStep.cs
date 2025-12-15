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

    public bool ValidateCalled { get; private set; }
    public bool ExecuteCalled { get; private set; }
    public bool RollbackCalled { get; private set; }

    public Task<InstallationResult> ValidateAsync(InstallationContext context)
    {
        ValidateCalled = true;
        CallHistory.Add("Validate");

        return Task.FromResult(ValidateShouldSucceed
            ? InstallationResult.SuccessResult($"{Name} validation succeeded")
            : InstallationResult.FailureResult($"{Name} validation failed"));
    }

    public async Task<InstallationResult> ExecuteAsync(InstallationContext context)
    {
        ExecuteCalled = true;
        CallHistory.Add("Execute");

        if (ExecutionDelay > TimeSpan.Zero)
            await Task.Delay(ExecutionDelay, context.CancellationToken);

        return ExecuteShouldSucceed
            ? InstallationResult.SuccessResult($"{Name} executed")
            : InstallationResult.FailureResult($"{Name} execution failed");
    }

    public Task<InstallationResult> RollbackAsync(InstallationContext context)
    {
        RollbackCalled = true;
        CallHistory.Add("Rollback");

        return Task.FromResult(RollbackShouldSucceed
            ? InstallationResult.SuccessResult($"{Name} rolled back")
            : InstallationResult.FailureResult($"{Name} rollback failed"));
    }
}
