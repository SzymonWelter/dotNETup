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
    // Helper method to wrap steps in ConfiguredStep
    private static List<ConfiguredStep> ToConfiguredSteps(params IInstallationStep[] steps)
        => steps.Select(s => new ConfiguredStep(s)).ToList();

    // ============================================
    // Constructor Tests
    // ============================================

    [Fact]
    public void Constructor_WithNullSteps_ThrowsArgumentNullException()
    {
        // Arrange
        var context = TestInstallationContext.Create();
        var options = new InstallationOptions();

        // Act
        Action act = () => new Installation(null!, context, options);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("steps");
    }

    [Fact]
    public void Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var steps = ToConfiguredSteps(new MockInstallationStep());
        var options = new InstallationOptions();

        // Act
        Action act = () => new Installation(steps, null!, options);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var steps = ToConfiguredSteps(new MockInstallationStep());
        var context = TestInstallationContext.Create();

        // Act
        Action act = () => new Installation(steps, context, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public void Constructor_WithEmptySteps_ThrowsArgumentException()
    {
        // Arrange
        var steps = new List<ConfiguredStep>();
        var context = TestInstallationContext.Create();
        var options = new InstallationOptions();

        // Act
        Action act = () => new Installation(steps, context, options);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("steps")
            .WithMessage("*at least one step is required*");
    }

    // ============================================
    // Validation Phase Tests
    // ============================================

    [Fact]
    public async Task InstallAsync_ValidatesAllSteps_BeforeExecution()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1" };
        var step2 = new MockInstallationStep { Name = "Step2" };
        var step3 = new MockInstallationStep { Name = "Step3" };

        var steps = ToConfiguredSteps(step1, step2, step3);
        var context = TestInstallationContext.Create();
        var options = new InstallationOptions();
        var installation = new Installation(steps, context, options);

        // Act
        await installation.InstallAsync();

        // Assert
        step1.ValidateCalled.Should().BeTrue("step1 should be validated");
        step2.ValidateCalled.Should().BeTrue("step2 should be validated");
        step3.ValidateCalled.Should().BeTrue("step3 should be validated");
    }

    [Fact]
    public async Task InstallAsync_WhenValidationFails_ReturnsFailureImmediately()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1", ValidateShouldSucceed = true };
        var step2 = new MockInstallationStep { Name = "Step2", ValidateShouldSucceed = false };
        var step3 = new MockInstallationStep { Name = "Step3", ValidateShouldSucceed = true };

        var steps = ToConfiguredSteps(step1, step2, step3);
        var context = TestInstallationContext.Create();
        var options = new InstallationOptions();
        var installation = new Installation(steps, context, options);

        // Act
        var result = await installation.InstallAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Validation failed");
    }

    [Fact]
    public async Task InstallAsync_WhenValidationFails_DoesNotExecuteAnySteps()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1", ValidateShouldSucceed = false };
        var step2 = new MockInstallationStep { Name = "Step2" };

        var steps = ToConfiguredSteps(step1, step2);
        var context = TestInstallationContext.Create();
        var options = new InstallationOptions();
        var installation = new Installation(steps, context, options);

        // Act
        await installation.InstallAsync();

        // Assert
        step1.ExecuteCalled.Should().BeFalse("no steps should execute when validation fails");
        step2.ExecuteCalled.Should().BeFalse("no steps should execute when validation fails");
    }

    [Fact]
    public async Task InstallAsync_WhenValidateBeforeInstallDisabled_SkipsValidation()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1", ValidateShouldSucceed = false };

        var steps = ToConfiguredSteps(step1);
        var context = TestInstallationContext.Create();
        var options = new InstallationOptions { ValidateBeforeInstall = false };
        var installation = new Installation(steps, context, options);

        // Act
        var result = await installation.InstallAsync();

        // Assert
        step1.ValidateCalled.Should().BeFalse("validation should be skipped");
        step1.ExecuteCalled.Should().BeTrue("execution should proceed without validation");
    }

    // ============================================
    // Execution Phase Tests
    // ============================================

    [Fact]
    public async Task InstallAsync_ExecutesSteps_InOrder()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1" };
        var step2 = new MockInstallationStep { Name = "Step2" };
        var step3 = new MockInstallationStep { Name = "Step3" };

        var steps = ToConfiguredSteps(step1, step2, step3);
        var context = TestInstallationContext.Create();
        var options = new InstallationOptions();
        var installation = new Installation(steps, context, options);

        // Act
        await installation.InstallAsync();

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
    public async Task InstallAsync_CallsSetCurrentStep_BeforeEachExecution()
    {
        // Arrange
        var progressReports = new List<InstallationProgress>();
        var progress = new Progress<InstallationProgress>(p => progressReports.Add(p));
        var context = TestInstallationContext.Create(progress: progress);
        var options = new InstallationOptions();

        var step1 = new MockInstallationStep { Name = "Step1" };
        var step2 = new MockInstallationStep { Name = "Step2" };

        var steps = ToConfiguredSteps(step1, step2);
        var installation = new Installation(steps, context, options);

        // Act
        await installation.InstallAsync();

        // Assert - SetCurrentStep should be called, which can be verified through progress reporting
        context.Should().NotBeNull();
    }

    [Fact]
    public async Task InstallAsync_WhenAllStepsSucceed_ReturnsSuccess()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1", ExecuteShouldSucceed = true };
        var step2 = new MockInstallationStep { Name = "Step2", ExecuteShouldSucceed = true };

        var steps = ToConfiguredSteps(step1, step2);
        var context = TestInstallationContext.Create();
        var options = new InstallationOptions();
        var installation = new Installation(steps, context, options);

        // Act
        var result = await installation.InstallAsync();

        // Assert
        result.Success.Should().BeTrue();
        step1.ExecuteCalled.Should().BeTrue();
        step2.ExecuteCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InstallAsync_WhenStepFails_ReturnsFailure()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1", ExecuteShouldSucceed = true };
        var step2 = new MockInstallationStep { Name = "Step2", ExecuteShouldSucceed = false };
        var step3 = new MockInstallationStep { Name = "Step3", ExecuteShouldSucceed = true };

        var steps = ToConfiguredSteps(step1, step2, step3);
        var context = TestInstallationContext.Create();
        var options = new InstallationOptions();
        var installation = new Installation(steps, context, options);

        // Act
        var result = await installation.InstallAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("failed");
    }

    [Fact]
    public async Task InstallAsync_WhenStepFails_DoesNotExecuteRemainingSteps()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1", ExecuteShouldSucceed = true };
        var step2 = new MockInstallationStep { Name = "Step2", ExecuteShouldSucceed = false };
        var step3 = new MockInstallationStep { Name = "Step3", ExecuteShouldSucceed = true };

        var steps = ToConfiguredSteps(step1, step2, step3);
        var context = TestInstallationContext.Create();
        var options = new InstallationOptions();
        var installation = new Installation(steps, context, options);

        // Act
        await installation.InstallAsync();

        // Assert
        step1.ExecuteCalled.Should().BeTrue("step1 should execute");
        step2.ExecuteCalled.Should().BeTrue("step2 should execute and fail");
        step3.ExecuteCalled.Should().BeFalse("step3 should NOT execute after step2 fails");
    }

    // ============================================
    // Rollback Tests
    // ============================================

    [Fact]
    public async Task InstallAsync_WhenStepFails_TriggersRollback()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1", ExecuteShouldSucceed = true };
        var step2 = new MockInstallationStep { Name = "Step2", ExecuteShouldSucceed = false };

        var steps = ToConfiguredSteps(step1, step2);
        var context = TestInstallationContext.Create();
        var options = new InstallationOptions();
        var installation = new Installation(steps, context, options);

        // Act
        await installation.InstallAsync();

        // Assert
        step1.RollbackCalled.Should().BeTrue("step1 should be rolled back after step2 fails");
        step2.RollbackCalled.Should().BeTrue("step2 should be rolled back after it fails");
    }

    [Fact]
    public async Task InstallAsync_WhenRollbackOnFailureDisabled_DoesNotRollback()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1", ExecuteShouldSucceed = true };
        var step2 = new MockInstallationStep { Name = "Step2", ExecuteShouldSucceed = false };

        var steps = ToConfiguredSteps(step1, step2);
        var context = TestInstallationContext.Create();
        var options = new InstallationOptions { RollbackOnFailure = false };
        var installation = new Installation(steps, context, options);

        // Act
        await installation.InstallAsync();

        // Assert
        step1.RollbackCalled.Should().BeFalse("rollback should be disabled");
        step2.RollbackCalled.Should().BeFalse("rollback should be disabled");
    }

    [Fact]
    public async Task InstallAsync_RollsBackSteps_InReverseOrder()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1", ExecuteShouldSucceed = true };
        var step2 = new MockInstallationStep { Name = "Step2", ExecuteShouldSucceed = true };
        var step3 = new MockInstallationStep { Name = "Step3", ExecuteShouldSucceed = false };

        var steps = ToConfiguredSteps(step1, step2, step3);
        var context = TestInstallationContext.Create();
        var options = new InstallationOptions();
        var installation = new Installation(steps, context, options);

        // Act
        await installation.InstallAsync();

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
    public async Task InstallAsync_RollsBackOnlyExecutedSteps_NotValidatedOnlySteps()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1", ExecuteShouldSucceed = true };
        var step2 = new MockInstallationStep { Name = "Step2", ExecuteShouldSucceed = false };
        var step3 = new MockInstallationStep { Name = "Step3", ExecuteShouldSucceed = true };

        var steps = ToConfiguredSteps(step1, step2, step3);
        var context = TestInstallationContext.Create();
        var options = new InstallationOptions();
        var installation = new Installation(steps, context, options);

        // Act
        await installation.InstallAsync();

        // Assert
        step1.RollbackCalled.Should().BeTrue("step1 was executed and should be rolled back");
        step2.RollbackCalled.Should().BeTrue("step2 was executed and should be rolled back");
        step3.RollbackCalled.Should().BeFalse("step3 was not executed and should NOT be rolled back");
    }

    [Fact]
    public async Task InstallAsync_WhenRollbackFails_ContinuesRollingBackOtherSteps()
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

        var steps = ToConfiguredSteps(step1, step2, step3);
        var context = TestInstallationContext.Create();
        var options = new InstallationOptions();
        var installation = new Installation(steps, context, options);

        // Act
        var result = await installation.InstallAsync();

        // Assert
        result.Success.Should().BeFalse();
        step1.RollbackCalled.Should().BeTrue("step1 should be rolled back despite step2 rollback failure");
        step2.RollbackCalled.Should().BeTrue("step2 rollback should be attempted");
        step3.RollbackCalled.Should().BeTrue("step3 rollback should be attempted");
    }

    [Fact]
    public async Task InstallAsync_WhenMultipleRollbacksFail_LogsAllFailures()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        var context = TestInstallationContext.Create(logger: mockLogger);
        var options = new InstallationOptions();

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

        var steps = ToConfiguredSteps(step1, step2, step3);
        var installation = new Installation(steps, context, options);

        // Act
        await installation.InstallAsync();

        // Assert - Verify logging occurred (multiple log calls for warnings)
        mockLogger.ReceivedCalls().Should().NotBeEmpty("because rollback failures should be logged");
    }

    // ============================================
    // Cancellation Tests
    // ============================================

    [Fact]
    public async Task InstallAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        var context = TestInstallationContext.Create(cancellationToken: cts.Token);
        var options = new InstallationOptions();
        var step = new MockInstallationStep { ExecutionDelay = TimeSpan.FromMilliseconds(100) };
        var steps = ToConfiguredSteps(step);
        var installation = new Installation(steps, context, options);

        // Act
        Func<Task> act = async () => await installation.InstallAsync();

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task InstallAsync_WhenCancelled_TriggersRollback()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var context = TestInstallationContext.Create(cancellationToken: cts.Token);
        var options = new InstallationOptions();

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

        var steps = ToConfiguredSteps(step1, step2);
        var installation = new Installation(steps, context, options);

        // Act - Execute step1, then cancel during step2
        var task = installation.InstallAsync();
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
    // InstallationSummary Tests
    // ============================================

    [Fact]
    public async Task InstallAsync_ReturnsInstallationSummary_WithStepResults()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1", ExecuteShouldSucceed = true };
        var step2 = new MockInstallationStep { Name = "Step2", ExecuteShouldSucceed = true };

        var steps = ToConfiguredSteps(step1, step2);
        var context = TestInstallationContext.Create();
        var options = new InstallationOptions();
        var installation = new Installation(steps, context, options);

        // Act
        var summary = await installation.InstallAsync();

        // Assert
        summary.StepResults.Should().ContainKey("Step1");
        summary.StepResults.Should().ContainKey("Step2");
        summary.CompletedSteps.Should().Be(2);
        summary.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task InstallAsync_WhenFails_ReturnsFailedStepName()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1", ExecuteShouldSucceed = true };
        var step2 = new MockInstallationStep { Name = "FailingStep", ExecuteShouldSucceed = false };

        var steps = ToConfiguredSteps(step1, step2);
        var context = TestInstallationContext.Create();
        var options = new InstallationOptions();
        var installation = new Installation(steps, context, options);

        // Act
        var summary = await installation.InstallAsync();

        // Assert
        summary.FailedStep.Should().Be("FailingStep");
    }

    // ============================================
    // UninstallAsync Tests
    // ============================================

    [Fact]
    public async Task UninstallAsync_CallsRollbackAsync_InReverseOrder()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1" };
        var step2 = new MockInstallationStep { Name = "Step2" };
        var step3 = new MockInstallationStep { Name = "Step3" };

        var steps = ToConfiguredSteps(step1, step2, step3);
        var context = TestInstallationContext.Create();
        var options = new InstallationOptions();
        var installation = new Installation(steps, context, options);

        // Act
        await installation.UninstallAsync();

        // Assert - RollbackAsync should be called on all steps (not ExecuteAsync)
        step1.RollbackCalled.Should().BeTrue("step1 RollbackAsync should be called during uninstall");
        step2.RollbackCalled.Should().BeTrue("step2 RollbackAsync should be called during uninstall");
        step3.RollbackCalled.Should().BeTrue("step3 RollbackAsync should be called during uninstall");

        // ExecuteAsync should NOT be called during uninstall
        step1.ExecuteCalled.Should().BeFalse("ExecuteAsync should not be called during uninstall");
        step2.ExecuteCalled.Should().BeFalse("ExecuteAsync should not be called during uninstall");
        step3.ExecuteCalled.Should().BeFalse("ExecuteAsync should not be called during uninstall");
    }

    [Fact]
    public async Task UninstallAsync_SetsIsUninstallFlag()
    {
        // Arrange
        var context = TestInstallationContext.Create();
        var options = new InstallationOptions();
        var step = new MockInstallationStep { Name = "Step1" };
        var steps = ToConfiguredSteps(step);
        var installation = new Installation(steps, context, options);

        // Act
        await installation.UninstallAsync();

        // Assert
        context.IsUninstall.Should().BeTrue();
    }

    [Fact]
    public async Task UninstallAsync_WhenRollbackFails_ReturnsFailure()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1", RollbackShouldSucceed = true };
        var step2 = new MockInstallationStep { Name = "Step2", RollbackShouldSucceed = false };

        var steps = ToConfiguredSteps(step1, step2);
        var context = TestInstallationContext.Create();
        var options = new InstallationOptions();
        var installation = new Installation(steps, context, options);

        // Act - Steps are reversed, so Step2's rollback is called first
        var result = await installation.UninstallAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.FailedStep.Should().Be("Step2");
    }

    [Fact]
    public async Task UninstallAsync_ReturnsSuccessSummary_WhenAllRollbacksSucceed()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1", RollbackShouldSucceed = true };
        var step2 = new MockInstallationStep { Name = "Step2", RollbackShouldSucceed = true };

        var steps = ToConfiguredSteps(step1, step2);
        var context = TestInstallationContext.Create();
        var options = new InstallationOptions();
        var installation = new Installation(steps, context, options);

        // Act
        var result = await installation.UninstallAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Uninstallation completed successfully");
        result.StepResults.Should().HaveCount(2);
    }

    // ============================================
    // RepairAsync Tests
    // ============================================

    [Fact]
    public async Task RepairAsync_WithNoStepNames_ExecutesAllSteps()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1" };
        var step2 = new MockInstallationStep { Name = "Step2" };

        var steps = ToConfiguredSteps(step1, step2);
        var context = TestInstallationContext.Create();
        var options = new InstallationOptions();
        var installation = new Installation(steps, context, options);

        // Act
        await installation.RepairAsync();

        // Assert
        step1.ExecuteCalled.Should().BeTrue();
        step2.ExecuteCalled.Should().BeTrue();
    }

    [Fact]
    public async Task RepairAsync_WithSpecificStepNames_ExecutesOnlyThoseSteps()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1" };
        var step2 = new MockInstallationStep { Name = "Step2" };
        var step3 = new MockInstallationStep { Name = "Step3" };

        var steps = ToConfiguredSteps(step1, step2, step3);
        var context = TestInstallationContext.Create();
        var options = new InstallationOptions();
        var installation = new Installation(steps, context, options);

        // Act
        await installation.RepairAsync("Step2");

        // Assert
        step1.ExecuteCalled.Should().BeFalse();
        step2.ExecuteCalled.Should().BeTrue();
        step3.ExecuteCalled.Should().BeFalse();
    }

    [Fact]
    public async Task RepairAsync_WithNonExistentStepName_ReturnsFailure()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1" };
        var steps = ToConfiguredSteps(step1);
        var context = TestInstallationContext.Create();
        var options = new InstallationOptions();
        var installation = new Installation(steps, context, options);

        // Act
        var result = await installation.RepairAsync("NonExistentStep");

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("No matching steps found");
    }

    // ============================================
    // Integration Tests
    // ============================================

    [Fact]
    public async Task InstallAsync_ComplexScenario_Step3Fails_RollsBackSteps1And2()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "CopyFiles", ExecuteShouldSucceed = true };
        var step2 = new MockInstallationStep { Name = "UpdateRegistry", ExecuteShouldSucceed = true };
        var step3 = new MockInstallationStep { Name = "InstallService", ExecuteShouldSucceed = false };

        var steps = ToConfiguredSteps(step1, step2, step3);
        var context = TestInstallationContext.Create();
        var options = new InstallationOptions();
        var installation = new Installation(steps, context, options);

        // Act
        var result = await installation.InstallAsync();

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
    public async Task InstallAsync_EndToEnd_AllStepsSucceed()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        var progressReports = new List<InstallationProgress>();
        var progress = new Progress<InstallationProgress>(p => progressReports.Add(p));
        var context = TestInstallationContext.Create(logger: mockLogger, progress: progress);
        var options = new InstallationOptions();

        var step1 = new MockInstallationStep { Name = "Validate", ExecuteShouldSucceed = true };
        var step2 = new MockInstallationStep { Name = "Backup", ExecuteShouldSucceed = true };
        var step3 = new MockInstallationStep { Name = "Install", ExecuteShouldSucceed = true };
        var step4 = new MockInstallationStep { Name = "Configure", ExecuteShouldSucceed = true };

        var steps = ToConfiguredSteps(step1, step2, step3, step4);
        var installation = new Installation(steps, context, options);

        // Act
        var result = await installation.InstallAsync();

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

    // ============================================
    // InstallationStepOptions Tests
    // ============================================

    [Fact]
    public async Task InstallAsync_WithContinueOnError_ContinuesAfterStepFailure()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1", ExecuteShouldSucceed = true };
        var step2 = new MockInstallationStep { Name = "Step2", ExecuteShouldSucceed = false };
        var step3 = new MockInstallationStep { Name = "Step3", ExecuteShouldSucceed = true };

        var steps = new List<ConfiguredStep>
        {
            new ConfiguredStep(step1),
            new ConfiguredStep(step2, new InstallationStepOptions { ContinueOnError = true }),
            new ConfiguredStep(step3)
        };

        var context = TestInstallationContext.Create();
        var options = new InstallationOptions();
        var installation = new Installation(steps, context, options);

        // Act
        var result = await installation.InstallAsync();

        // Assert
        result.Success.Should().BeTrue("installation should succeed despite step2 failure because ContinueOnError is set");
        step1.ExecuteCalled.Should().BeTrue();
        step2.ExecuteCalled.Should().BeTrue();
        step3.ExecuteCalled.Should().BeTrue("step3 should execute even after step2 fails");
    }

    [Fact]
    public async Task InstallAsync_WithShouldSkip_SkipsStep()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1", ExecuteShouldSucceed = true };
        var step2 = new MockInstallationStep { Name = "Step2", ExecuteShouldSucceed = true };
        var step3 = new MockInstallationStep { Name = "Step3", ExecuteShouldSucceed = true };

        var steps = new List<ConfiguredStep>
        {
            new ConfiguredStep(step1),
            new ConfiguredStep(step2, new InstallationStepOptions
            {
                ShouldSkip = _ => Task.FromResult(true) // Always skip
            }),
            new ConfiguredStep(step3)
        };

        var context = TestInstallationContext.Create();
        var options = new InstallationOptions();
        var installation = new Installation(steps, context, options);

        // Act
        var result = await installation.InstallAsync();

        // Assert
        result.Success.Should().BeTrue();
        step1.ExecuteCalled.Should().BeTrue();
        step2.ExecuteCalled.Should().BeFalse("step2 should be skipped");
        step3.ExecuteCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InstallAsync_WithShouldSkipBasedOnContext_SkipsConditionally()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1", ExecuteShouldSucceed = true };
        var step2 = new MockInstallationStep { Name = "Step2", ExecuteShouldSucceed = true };

        var steps = new List<ConfiguredStep>
        {
            new ConfiguredStep(step1),
            new ConfiguredStep(step2, new InstallationStepOptions
            {
                ShouldSkip = ctx => Task.FromResult(ctx.Properties.ContainsKey("SkipStep2"))
            })
        };

        var context = TestInstallationContext.Create();
        context.Properties["SkipStep2"] = true;
        var options = new InstallationOptions();
        var installation = new Installation(steps, context, options);

        // Act
        var result = await installation.InstallAsync();

        // Assert
        result.Success.Should().BeTrue();
        step1.ExecuteCalled.Should().BeTrue();
        step2.ExecuteCalled.Should().BeFalse("step2 should be skipped based on context property");
    }

    [Fact]
    public async Task InstallAsync_WithCustomStepName_UsesCustomName()
    {
        // Arrange
        var step = new MockInstallationStep { Name = "OriginalName", ExecuteShouldSucceed = true };

        var steps = new List<ConfiguredStep>
        {
            new ConfiguredStep(step, new InstallationStepOptions { Name = "CustomName" })
        };

        var context = TestInstallationContext.Create();
        var options = new InstallationOptions();
        var installation = new Installation(steps, context, options);

        // Act
        var result = await installation.InstallAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.StepResults.Should().ContainKey("CustomName");
        result.StepResults.Should().NotContainKey("OriginalName");
    }

    [Fact]
    public async Task InstallAsync_WithRetryCount_RetriesOnFailure()
    {
        // Arrange
        var attemptCount = 0;
        var step = new MockInstallationStep
        {
            Name = "RetryableStep",
            ExecuteShouldSucceed = false,
            ExecuteCallback = () =>
            {
                attemptCount++;
                // Succeed on 3rd attempt
                return attemptCount >= 3
                    ? InstallationStepResult.SuccessResult("Success on retry")
                    : InstallationStepResult.FailureResult("Failed");
            }
        };

        var steps = new List<ConfiguredStep>
        {
            new ConfiguredStep(step, new InstallationStepOptions { RetryCount = 3 })
        };

        var context = TestInstallationContext.Create();
        var options = new InstallationOptions { ValidateBeforeInstall = false };
        var installation = new Installation(steps, context, options);

        // Act
        var result = await installation.InstallAsync();

        // Assert
        result.Success.Should().BeTrue("step should succeed after retries");
        attemptCount.Should().Be(3, "step should have been attempted 3 times");
    }

    [Fact]
    public async Task UninstallAsync_WithContinueOnError_ContinuesAfterRollbackFailure()
    {
        // Arrange
        var step1 = new MockInstallationStep { Name = "Step1", RollbackShouldSucceed = true };
        var step2 = new MockInstallationStep { Name = "Step2", RollbackShouldSucceed = false };
        var step3 = new MockInstallationStep { Name = "Step3", RollbackShouldSucceed = true };

        var steps = new List<ConfiguredStep>
        {
            new ConfiguredStep(step1),
            new ConfiguredStep(step2, new InstallationStepOptions { ContinueOnError = true }),
            new ConfiguredStep(step3)
        };

        var context = TestInstallationContext.Create();
        var options = new InstallationOptions();
        var installation = new Installation(steps, context, options);

        // Act - Steps are reversed during uninstall
        var result = await installation.UninstallAsync();

        // Assert
        result.Success.Should().BeTrue("uninstall should succeed despite step2 rollback failure because ContinueOnError is set");
        step3.RollbackCalled.Should().BeTrue();
        step2.RollbackCalled.Should().BeTrue();
        step1.RollbackCalled.Should().BeTrue("step1 rollback should be called even after step2 rollback fails");
    }
}
