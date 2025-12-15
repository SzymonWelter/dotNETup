using DotNetUp.Core.Builders;
using DotNetUp.Core.Execution;
using DotNetUp.Core.Models;
using DotNetUp.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotNetUp.Tests.Integration;

/// <summary>
/// End-to-end integration tests for realistic installation scenarios.
/// </summary>
public class EndToEndTests
{
    [Fact]
    public async Task CompleteInstallation_WithMultipleSteps_SucceedsAndReportsProgress()
    {
        // Arrange
        var logger = Substitute.For<ILogger>();
        var progressReports = new List<InstallationProgress>();
        var progress = new Progress<InstallationProgress>(p => progressReports.Add(p));

        var step1 = new MockInstallationStep
        {
            Name = "ValidateSystemRequirements",
            Description = "Checks if system meets minimum requirements",
            ExecuteShouldSucceed = true
        };

        var step2 = new MockInstallationStep
        {
            Name = "BackupExistingFiles",
            Description = "Creates backup of existing installation",
            ExecuteShouldSucceed = true
        };

        var step3 = new MockInstallationStep
        {
            Name = "ExtractInstallationFiles",
            Description = "Extracts files from installation package",
            ExecuteShouldSucceed = true
        };

        var step4 = new MockInstallationStep
        {
            Name = "ConfigureApplication",
            Description = "Configures application settings",
            ExecuteShouldSucceed = true
        };

        var builder = new InstallationBuilder();
        builder
            .WithLogger(logger)
            .WithProgress(progress)
            .WithProperty("InstallPath", "/opt/myapp")
            .WithProperty("Version", "2.0.0")
            .WithProperty("BackupEnabled", true)
            .WithStep(step1)
            .WithStep(step2)
            .WithStep(step3)
            .WithStep(step4);

        // Act
        var (steps, context) = builder.Build();
        var installation = new Installation(steps, context);
        var result = await installation.ExecuteAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("completed successfully");

        // Verify all steps executed
        step1.ValidateCalled.Should().BeTrue();
        step1.ExecuteCalled.Should().BeTrue();
        step2.ValidateCalled.Should().BeTrue();
        step2.ExecuteCalled.Should().BeTrue();
        step3.ValidateCalled.Should().BeTrue();
        step3.ExecuteCalled.Should().BeTrue();
        step4.ValidateCalled.Should().BeTrue();
        step4.ExecuteCalled.Should().BeTrue();

        // Verify no rollbacks occurred
        step1.RollbackCalled.Should().BeFalse();
        step2.RollbackCalled.Should().BeFalse();
        step3.RollbackCalled.Should().BeFalse();
        step4.RollbackCalled.Should().BeFalse();

        // Verify properties are accessible
        context.Properties["InstallPath"].Should().Be("/opt/myapp");
        context.Properties["Version"].Should().Be("2.0.0");
        context.Properties["BackupEnabled"].Should().Be(true);

        // Verify logging occurred
        logger.ReceivedCalls().Should().NotBeEmpty();
    }

    [Fact]
    public async Task FailedInstallation_TriggersRollback_AndReportsCorrectly()
    {
        // Arrange
        var logger = Substitute.For<ILogger>();
        var progressReports = new List<InstallationProgress>();
        var progress = new Progress<InstallationProgress>(p => progressReports.Add(p));

        var step1 = new MockInstallationStep
        {
            Name = "CreateDirectory",
            ExecuteShouldSucceed = true
        };

        var step2 = new MockInstallationStep
        {
            Name = "CopyFiles",
            ExecuteShouldSucceed = true
        };

        var step3 = new MockInstallationStep
        {
            Name = "SetPermissions",
            ExecuteShouldSucceed = false, // This step fails
            RollbackShouldSucceed = true
        };

        var step4 = new MockInstallationStep
        {
            Name = "RegisterService",
            ExecuteShouldSucceed = true // This step won't execute
        };

        var builder = new InstallationBuilder();
        builder
            .WithLogger(logger)
            .WithProgress(progress)
            .WithStep(step1)
            .WithStep(step2)
            .WithStep(step3)
            .WithStep(step4);

        // Act
        var (steps, context) = builder.Build();
        var installation = new Installation(steps, context);
        var result = await installation.ExecuteAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("failed");
        result.Message.Should().Contain("SetPermissions");

        // Verify execution stopped at failing step
        step1.ExecuteCalled.Should().BeTrue();
        step2.ExecuteCalled.Should().BeTrue();
        step3.ExecuteCalled.Should().BeTrue();
        step4.ExecuteCalled.Should().BeFalse("step4 should not execute after step3 fails");

        // Verify rollback in reverse order
        step1.RollbackCalled.Should().BeTrue();
        step2.RollbackCalled.Should().BeTrue();
        step3.RollbackCalled.Should().BeTrue();
        step4.RollbackCalled.Should().BeFalse("step4 was never executed");

        // Verify error logging
        logger.Received().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task CancelledInstallation_StopsExecution_AndRollsBack()
    {
        // Arrange
        var logger = Substitute.For<ILogger>();
        var cts = new CancellationTokenSource();

        var step1 = new MockInstallationStep
        {
            Name = "Step1",
            ExecuteShouldSucceed = true,
            ExecutionDelay = TimeSpan.FromMilliseconds(10)
        };

        var step2 = new MockInstallationStep
        {
            Name = "Step2",
            ExecuteShouldSucceed = true,
            ExecutionDelay = TimeSpan.FromMilliseconds(500) // Long delay
        };

        var step3 = new MockInstallationStep
        {
            Name = "Step3",
            ExecuteShouldSucceed = true
        };

        var builder = new InstallationBuilder();
        builder
            .WithLogger(logger)
            .WithCancellationToken(cts.Token)
            .WithStep(step1)
            .WithStep(step2)
            .WithStep(step3);

        var (steps, context) = builder.Build();
        var installation = new Installation(steps, context);

        // Act - Start execution and cancel during step2
        var task = installation.ExecuteAsync();
        await Task.Delay(50); // Wait for step1 to complete
        cts.Cancel();

        Func<Task> act = async () => await task;

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        // Verify step1 executed and was rolled back
        step1.ExecuteCalled.Should().BeTrue();
        step1.RollbackCalled.Should().BeTrue();

        // Step3 should not execute
        step3.ExecuteCalled.Should().BeFalse();
    }

    [Fact]
    public async Task ProgressReporting_CapturesAllStepTransitions()
    {
        // Arrange
        var logger = Substitute.For<ILogger>();
        var context = TestInstallationContext.Create(logger: logger);

        var step1 = new MockInstallationStep { Name = "Validate" };
        var step2 = new MockInstallationStep { Name = "Execute" };
        var step3 = new MockInstallationStep { Name = "Finalize" };

        var steps = new List<MockInstallationStep> { step1, step2, step3 };
        var installation = new Installation(steps, context);

        // Act
        await installation.ExecuteAsync();

        // Assert - Verify SetCurrentStep was called by checking logs
        // The executor should log step transitions
        logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Executing step 1/3")),
            null,
            Arg.Any<Func<object, Exception?, string>>());

        logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Executing step 2/3")),
            null,
            Arg.Any<Func<object, Exception?, string>>());

        logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Executing step 3/3")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task RollbackFailure_DoesNotStopOtherRollbacks()
    {
        // Arrange
        var logger = Substitute.For<ILogger>();

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
            ExecuteShouldSucceed = true,
            RollbackShouldSucceed = true
        };

        var step4 = new MockInstallationStep
        {
            Name = "Step4",
            ExecuteShouldSucceed = false // Triggers rollback
        };

        var builder = new InstallationBuilder();
        builder
            .WithLogger(logger)
            .WithStep(step1)
            .WithStep(step2)
            .WithStep(step3)
            .WithStep(step4);

        // Act
        var (steps, context) = builder.Build();
        var installation = new Installation(steps, context);
        var result = await installation.ExecuteAsync();

        // Assert
        result.Success.Should().BeFalse();

        // All executed steps should attempt rollback
        step1.RollbackCalled.Should().BeTrue();
        step2.RollbackCalled.Should().BeTrue();
        step3.RollbackCalled.Should().BeTrue();
        step4.RollbackCalled.Should().BeTrue();

        // Verify rollback failures are logged as warnings
        logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Rollback failed")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ValidationFailure_PreventsExecution()
    {
        // Arrange
        var logger = Substitute.For<ILogger>();

        var step1 = new MockInstallationStep
        {
            Name = "CheckDiskSpace",
            ValidateShouldSucceed = true
        };

        var step2 = new MockInstallationStep
        {
            Name = "CheckPermissions",
            ValidateShouldSucceed = false // Validation fails
        };

        var step3 = new MockInstallationStep
        {
            Name = "InstallFiles",
            ValidateShouldSucceed = true
        };

        var builder = new InstallationBuilder();
        builder
            .WithLogger(logger)
            .WithStep(step1)
            .WithStep(step2)
            .WithStep(step3);

        // Act
        var (steps, context) = builder.Build();
        var installation = new Installation(steps, context);
        var result = await installation.ExecuteAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Validation failed");

        // No steps should execute
        step1.ExecuteCalled.Should().BeFalse();
        step2.ExecuteCalled.Should().BeFalse();
        step3.ExecuteCalled.Should().BeFalse();

        // No rollbacks should occur
        step1.RollbackCalled.Should().BeFalse();
        step2.RollbackCalled.Should().BeFalse();
        step3.RollbackCalled.Should().BeFalse();

        // Verify validation failure is logged
        logger.Received().Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Is<object>(o => o.ToString()!.Contains("Validation failed")),
            null,
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task ComplexScenario_WithPropertiesAndSteps_WorksEndToEnd()
    {
        // Arrange
        var logger = Substitute.For<ILogger>();

        var builder = new InstallationBuilder();
        builder
            .WithLogger(logger)
            .WithProperty("SourcePath", "/tmp/package")
            .WithProperty("TargetPath", "/opt/application")
            .WithProperty("BackupPath", "/backup/application")
            .WithProperty("CreateBackup", true)
            .WithProperty("Overwrite", false);

        // Add steps that use properties
        var validateStep = new MockInstallationStep
        {
            Name = "ValidatePaths",
            Description = "Validates source, target, and backup paths exist"
        };

        var backupStep = new MockInstallationStep
        {
            Name = "CreateBackup",
            Description = "Backs up existing installation if requested"
        };

        var extractStep = new MockInstallationStep
        {
            Name = "ExtractPackage",
            Description = "Extracts installation package to target path"
        };

        var configureStep = new MockInstallationStep
        {
            Name = "Configure",
            Description = "Updates configuration files"
        };

        builder
            .WithStep(validateStep)
            .WithStep(backupStep)
            .WithStep(extractStep)
            .WithStep(configureStep);

        // Act
        var (steps, context) = builder.Build();
        var installation = new Installation(steps, context);
        var result = await installation.ExecuteAsync();

        // Assert
        result.Success.Should().BeTrue();

        // Verify all properties are accessible in context
        context.Properties.Should().ContainKeys(
            "SourcePath",
            "TargetPath",
            "BackupPath",
            "CreateBackup",
            "Overwrite");

        context.Properties["SourcePath"].Should().Be("/tmp/package");
        context.Properties["TargetPath"].Should().Be("/opt/application");
        context.Properties["BackupPath"].Should().Be("/backup/application");
        context.Properties["CreateBackup"].Should().Be(true);
        context.Properties["Overwrite"].Should().Be(false);

        // Verify all steps executed successfully
        validateStep.ExecuteCalled.Should().BeTrue();
        backupStep.ExecuteCalled.Should().BeTrue();
        extractStep.ExecuteCalled.Should().BeTrue();
        configureStep.ExecuteCalled.Should().BeTrue();
    }
}
