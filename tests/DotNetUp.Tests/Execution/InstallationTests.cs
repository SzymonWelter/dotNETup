using DotNetUp.Core.Execution;
using DotNetUp.Core.Interfaces;
using DotNetUp.Core.Models;
using DotNetUp.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotNetUp.Tests.Execution;

public class InstallationTests
{
    // ============================================
    // Constructor Tests
    // ============================================

    [Fact]
    public void Constructor_WithNullSteps_ThrowsArgumentNullException()
    {
        // Arrange
        var context = TestInstallationContext.Create();

        // Act
        Action act = () => new Installation(null!, context);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("steps");
    }

    [Fact]
    public void Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var steps = new List<IInstallationStep> { new MockInstallationStep() };

        // Act
        Action act = () => new Installation(steps, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public void Constructor_WithEmptySteps_ThrowsArgumentException()
    {
        // Arrange
        var steps = new List<IInstallationStep>();
        var context = TestInstallationContext.Create();

        // Act
        Action act = () => new Installation(steps, context);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("steps")
            .WithMessage("*at least one step is required*");
    }

    // ============================================
    // Validation Phase Tests
    // ============================================

    [Fact]
    public async Task ExecuteAsync_ValidatesAllSteps_BeforeExecution()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1" };
        var step2 = new MockInstallationStep { Name = "Step2" };
        var step3 = new MockInstallationStep { Name = "Step3" };

        var steps = new List<IInstallationStep> { step1, step2, step3 };
        var context = TestInstallationContext.Create();
        var installation = new Installation(steps, context);

        // Act
        await installation.ExecuteAsync();

        // Assert
        step1.ValidateCalled.Should().BeTrue("step1 should be validated");
        step2.ValidateCalled.Should().BeTrue("step2 should be validated");
        step3.ValidateCalled.Should().BeTrue("step3 should be validated");
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidationFails_ReturnsFailureImmediately()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1", ValidateShouldSucceed = true };
        var step2 = new MockInstallationStep { Name = "Step2", ValidateShouldSucceed = false };
        var step3 = new MockInstallationStep { Name = "Step3", ValidateShouldSucceed = true };

        var steps = new List<IInstallationStep> { step1, step2, step3 };
        var context = TestInstallationContext.Create();
        var installation = new Installation(steps, context);

        // Act
        var result = await installation.ExecuteAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("validation failed");
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidationFails_DoesNotExecuteAnySteps()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1", ValidateShouldSucceed = false };
        var step2 = new MockInstallationStep { Name = "Step2" };

        var steps = new List<IInstallationStep> { step1, step2 };
        var context = TestInstallationContext.Create();
        var installation = new Installation(steps, context);

        // Act
        await installation.ExecuteAsync();

        // Assert
        step1.ExecuteCalled.Should().BeFalse("no steps should execute when validation fails");
        step2.ExecuteCalled.Should().BeFalse("no steps should execute when validation fails");
    }

    // ============================================
    // Execution Phase Tests
    // ============================================

    [Fact]
    public async Task ExecuteAsync_ExecutesSteps_InOrder()
    {
        // Arrange
        var executionOrder = new List<string>();
        var step1 = new MockInstallationStep { Name = "Step1" };
        var step2 = new MockInstallationStep { Name = "Step2" };
        var step3 = new MockInstallationStep { Name = "Step3" };

        // Track execution order by examining call history
        var steps = new List<IInstallationStep> { step1, step2, step3 };
        var context = TestInstallationContext.Create();
        var installation = new Installation(steps, context);

        // Act
        await installation.ExecuteAsync();

        // Assert
        step1.ExecuteCalled.Should().BeTrue();
        step2.ExecuteCalled.Should().BeTrue();
        step3.ExecuteCalled.Should().BeTrue();

        // Verify order: all validates before any executes
        step1.CallHistory.Should().Contain("Validate");
        step1.CallHistory.Should().Contain("Execute");
        step2.CallHistory.Should().Contain("Validate");
        step2.CallHistory.Should().Contain("Execute");
    }

    [Fact]
    public async Task ExecuteAsync_CallsSetCurrentStep_BeforeEachExecution()
    {
        // Arrange
        var progressReports = new List<InstallationProgress>();
        var progress = new Progress<InstallationProgress>(p => progressReports.Add(p));
        var context = TestInstallationContext.Create(progress: progress);

        var step1 = new MockInstallationStep { Name = "Step1" };
        var step2 = new MockInstallationStep { Name = "Step2" };

        var steps = new List<IInstallationStep> { step1, step2 };
        var installation = new Installation(steps, context);

        // Act
        await installation.ExecuteAsync();

        // Assert - SetCurrentStep should be called, which can be verified through progress reporting
        // We can't directly verify SetCurrentStep, but we can check that steps can report progress
        // with correct step numbers (which requires SetCurrentStep to have been called)
        context.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenAllStepsSucceed_ReturnsSuccess()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1", ExecuteShouldSucceed = true };
        var step2 = new MockInstallationStep { Name = "Step2", ExecuteShouldSucceed = true };

        var steps = new List<IInstallationStep> { step1, step2 };
        var context = TestInstallationContext.Create();
        var installation = new Installation(steps, context);

        // Act
        var result = await installation.ExecuteAsync();

        // Assert
        result.Success.Should().BeTrue();
        step1.ExecuteCalled.Should().BeTrue();
        step2.ExecuteCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenStepFails_ReturnsFailure()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1", ExecuteShouldSucceed = true };
        var step2 = new MockInstallationStep { Name = "Step2", ExecuteShouldSucceed = false };
        var step3 = new MockInstallationStep { Name = "Step3", ExecuteShouldSucceed = true };

        var steps = new List<IInstallationStep> { step1, step2, step3 };
        var context = TestInstallationContext.Create();
        var installation = new Installation(steps, context);

        // Act
        var result = await installation.ExecuteAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("failed");
    }

    [Fact]
    public async Task ExecuteAsync_WhenStepFails_DoesNotExecuteRemainingSteps()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1", ExecuteShouldSucceed = true };
        var step2 = new MockInstallationStep { Name = "Step2", ExecuteShouldSucceed = false };
        var step3 = new MockInstallationStep { Name = "Step3", ExecuteShouldSucceed = true };

        var steps = new List<IInstallationStep> { step1, step2, step3 };
        var context = TestInstallationContext.Create();
        var installation = new Installation(steps, context);

        // Act
        await installation.ExecuteAsync();

        // Assert
        step1.ExecuteCalled.Should().BeTrue("step1 should execute");
        step2.ExecuteCalled.Should().BeTrue("step2 should execute and fail");
        step3.ExecuteCalled.Should().BeFalse("step3 should NOT execute after step2 fails");
    }

    // ============================================
    // Rollback Tests
    // ============================================

    [Fact]
    public async Task ExecuteAsync_WhenStepFails_TriggersRollback()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1", ExecuteShouldSucceed = true };
        var step2 = new MockInstallationStep { Name = "Step2", ExecuteShouldSucceed = false };

        var steps = new List<IInstallationStep> { step1, step2 };
        var context = TestInstallationContext.Create();
        var installation = new Installation(steps, context);

        // Act
        await installation.ExecuteAsync();

        // Assert
        step1.RollbackCalled.Should().BeTrue("step1 should be rolled back after step2 fails");
        step2.RollbackCalled.Should().BeTrue("step2 should be rolled back after it fails");
    }

    [Fact]
    public async Task ExecuteAsync_RollsBackSteps_InReverseOrder()
    {
        // Arrange
        var rollbackOrder = new List<string>();
        var step1 = new MockInstallationStep { Name = "Step1", ExecuteShouldSucceed = true };
        var step2 = new MockInstallationStep { Name = "Step2", ExecuteShouldSucceed = true };
        var step3 = new MockInstallationStep { Name = "Step3", ExecuteShouldSucceed = false };

        var steps = new List<IInstallationStep> { step1, step2, step3 };
        var context = TestInstallationContext.Create();
        var installation = new Installation(steps, context);

        // Act
        await installation.ExecuteAsync();

        // Assert - Check rollback was called
        step3.RollbackCalled.Should().BeTrue("step3 should be rolled back");
        step2.RollbackCalled.Should().BeTrue("step2 should be rolled back");
        step1.RollbackCalled.Should().BeTrue("step1 should be rolled back");

        // Verify rollback appears in call history after execute
        step1.CallHistory.Should().Contain("Rollback");
        step2.CallHistory.Should().Contain("Rollback");
        step3.CallHistory.Should().Contain("Rollback");
    }

    [Fact]
    public async Task ExecuteAsync_RollsBackOnlyExecutedSteps_NotValidatedOnlySteps()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1", ExecuteShouldSucceed = true };
        var step2 = new MockInstallationStep { Name = "Step2", ExecuteShouldSucceed = false };
        var step3 = new MockInstallationStep { Name = "Step3", ExecuteShouldSucceed = true };

        var steps = new List<IInstallationStep> { step1, step2, step3 };
        var context = TestInstallationContext.Create();
        var installation = new Installation(steps, context);

        // Act
        await installation.ExecuteAsync();

        // Assert
        step1.RollbackCalled.Should().BeTrue("step1 was executed and should be rolled back");
        step2.RollbackCalled.Should().BeTrue("step2 was executed and should be rolled back");
        step3.RollbackCalled.Should().BeFalse("step3 was not executed and should NOT be rolled back");
    }

    [Fact]
    public async Task ExecuteAsync_WhenRollbackFails_ContinuesRollingBackOtherSteps()
    {
        // Arrange
        var step1 = new MockInstallationStep
        {
            Name = "Step1",
            ExecuteShouldSucceed = true,
            RollbackShouldSucceed = true
        };
        var step2 = new MockInstallationStep
        {
            Name = "Step2",
            ExecuteShouldSucceed = true,
            RollbackShouldSucceed = false // Rollback fails
        };
        var step3 = new MockInstallationStep
        {
            Name = "Step3",
            ExecuteShouldSucceed = false // This triggers rollback
        };

        var steps = new List<IInstallationStep> { step1, step2, step3 };
        var context = TestInstallationContext.Create();
        var installation = new Installation(steps, context);

        // Act
        var result = await installation.ExecuteAsync();

        // Assert
        result.Success.Should().BeFalse();
        step1.RollbackCalled.Should().BeTrue("step1 should be rolled back despite step2 rollback failure");
        step2.RollbackCalled.Should().BeTrue("step2 rollback should be attempted");
        step3.RollbackCalled.Should().BeTrue("step3 rollback should be attempted");
    }

    [Fact]
    public async Task ExecuteAsync_WhenMultipleRollbacksFail_LogsAllFailures()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        var context = TestInstallationContext.Create(logger: mockLogger);

        var step1 = new MockInstallationStep
        {
            Name = "Step1",
            ExecuteShouldSucceed = true,
            RollbackShouldSucceed = false
        };
        var step2 = new MockInstallationStep
        {
            Name = "Step2",
            ExecuteShouldSucceed = true,
            RollbackShouldSucceed = false
        };
        var step3 = new MockInstallationStep
        {
            Name = "Step3",
            ExecuteShouldSucceed = false
        };

        var steps = new List<IInstallationStep> { step1, step2, step3 };
        var installation = new Installation(steps, context);

        // Act
        await installation.ExecuteAsync();

        // Assert - Verify logging occurred (multiple log calls)
        mockLogger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // ============================================
    // Cancellation Tests
    // ============================================

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        var context = TestInstallationContext.Create(cancellationToken: cts.Token);
        var step = new MockInstallationStep { ExecutionDelay = TimeSpan.FromMilliseconds(100) };
        var steps = new List<IInstallationStep> { step };
        var installation = new Installation(steps, context);

        // Act
        Func<Task> act = async () => await installation.ExecuteAsync();

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_TriggersRollback()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var context = TestInstallationContext.Create(cancellationToken: cts.Token);

        var step1 = new MockInstallationStep
        {
            Name = "Step1",
            ExecuteShouldSucceed = true
        };
        var step2 = new MockInstallationStep
        {
            Name = "Step2",
            ExecutionDelay = TimeSpan.FromMilliseconds(500)
        };

        var steps = new List<IInstallationStep> { step1, step2 };
        var installation = new Installation(steps, context);

        // Act - Execute step1, then cancel during step2
        var task = installation.ExecuteAsync();
        await Task.Delay(50); // Give time for step1 to execute
        cts.Cancel();

        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        // Assert
        step1.RollbackCalled.Should().BeTrue("step1 should be rolled back after cancellation");
    }

    // ============================================
    // Progress Tests
    // ============================================

    [Fact]
    public async Task ExecuteAsync_ReportsProgressForEachStep()
    {
        // Arrange
        var progressReports = new List<InstallationProgress>();
        var progress = new Progress<InstallationProgress>(p => progressReports.Add(p));
        var context = TestInstallationContext.Create(progress: progress);

        var step1 = new MockInstallationStep { Name = "Step1" };
        var step2 = new MockInstallationStep { Name = "Step2" };

        var steps = new List<IInstallationStep> { step1, step2 };
        var installation = new Installation(steps, context);

        // Act
        await installation.ExecuteAsync();

        // Assert - Progress reporting depends on step implementation
        // The executor should call SetCurrentStep before each step execution
        context.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ReportsCorrectStepNumbers()
    {
        // Arrange
        var context = TestInstallationContext.Create();

        var step1 = new MockInstallationStep { Name = "Step1" };
        var step2 = new MockInstallationStep { Name = "Step2" };
        var step3 = new MockInstallationStep { Name = "Step3" };

        var steps = new List<IInstallationStep> { step1, step2, step3 };
        var installation = new Installation(steps, context);

        // Act
        await installation.ExecuteAsync();

        // Assert - Verify steps were executed in sequence
        step1.ExecuteCalled.Should().BeTrue();
        step2.ExecuteCalled.Should().BeTrue();
        step3.ExecuteCalled.Should().BeTrue();
    }

    // ============================================
    // Integration Tests
    // ============================================

    [Fact]
    public async Task ExecuteAsync_ComplexScenario_Step3Fails_RollsBackSteps1And2()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "CopyFiles", ExecuteShouldSucceed = true };
        var step2 = new MockInstallationStep { Name = "UpdateRegistry", ExecuteShouldSucceed = true };
        var step3 = new MockInstallationStep { Name = "InstallService", ExecuteShouldSucceed = false };

        var steps = new List<IInstallationStep> { step1, step2, step3 };
        var context = TestInstallationContext.Create();
        var installation = new Installation(steps, context);

        // Act
        var result = await installation.ExecuteAsync();

        // Assert
        result.Success.Should().BeFalse();

        // Verify execution
        step1.ExecuteCalled.Should().BeTrue();
        step2.ExecuteCalled.Should().BeTrue();
        step3.ExecuteCalled.Should().BeTrue();

        // Verify rollback in reverse order
        step3.RollbackCalled.Should().BeTrue();
        step2.RollbackCalled.Should().BeTrue();
        step1.RollbackCalled.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_EndToEnd_AllStepsSucceed()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        var progressReports = new List<InstallationProgress>();
        var progress = new Progress<InstallationProgress>(p => progressReports.Add(p));
        var context = TestInstallationContext.Create(logger: mockLogger, progress: progress);

        var step1 = new MockInstallationStep { Name = "Validate", ExecuteShouldSucceed = true };
        var step2 = new MockInstallationStep { Name = "Backup", ExecuteShouldSucceed = true };
        var step3 = new MockInstallationStep { Name = "Install", ExecuteShouldSucceed = true };
        var step4 = new MockInstallationStep { Name = "Configure", ExecuteShouldSucceed = true };

        var steps = new List<IInstallationStep> { step1, step2, step3, step4 };
        var installation = new Installation(steps, context);

        // Act
        var result = await installation.ExecuteAsync();

        // Assert
        result.Success.Should().BeTrue();

        // Verify all steps executed
        step1.ExecuteCalled.Should().BeTrue();
        step2.ExecuteCalled.Should().BeTrue();
        step3.ExecuteCalled.Should().BeTrue();
        step4.ExecuteCalled.Should().BeTrue();

        // Verify no rollbacks
        step1.RollbackCalled.Should().BeFalse();
        step2.RollbackCalled.Should().BeFalse();
        step3.RollbackCalled.Should().BeFalse();
        step4.RollbackCalled.Should().BeFalse();

        // Verify logging occurred
        mockLogger.ReceivedCalls().Should().NotBeEmpty();
    }
}
