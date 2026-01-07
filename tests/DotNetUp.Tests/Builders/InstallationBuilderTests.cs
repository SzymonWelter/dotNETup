using DotNetUp.Core.Builders;
using DotNetUp.Core.Execution;
using DotNetUp.Core.Interfaces;
using DotNetUp.Core.Models;
using DotNetUp.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotNetUp.Tests.Builders;

public class InstallationBuilderTests
{
    [Fact]
    public void WithStep_AddsStepToBuilder()
    {
        // Arrange
        var builder = new InstallationBuilder();
        var step = new MockInstallationStep { Name = "TestStep" };

        // Act
        var result = builder.WithStep(step);

        // Assert
        result.Should().Be(builder, "builder should return itself for fluent chaining");
    }

    [Fact]
    public void WithStep_WithNullStep_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new InstallationBuilder();

        // Act
        Action act = () => builder.WithStep(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("step");
    }

    [Fact]
    public void WithProperty_AddsPropertyToBuilder()
    {
        // Arrange
        var builder = new InstallationBuilder();

        // Act
        var result = builder
            .WithProperty("Key1", "Value1")
            .WithProperty("Key2", 42);

        // Assert
        result.Should().Be(builder, "builder should return itself for fluent chaining");
    }

    [Fact]
    public void WithProperty_WithNullOrEmptyKey_ThrowsArgumentException()
    {
        // Arrange
        var builder = new InstallationBuilder();

        // Act
        Action actNull = () => builder.WithProperty(null!, "value");
        Action actEmpty = () => builder.WithProperty(string.Empty, "value");
        Action actWhitespace = () => builder.WithProperty("  ", "value");

        // Assert
        actNull.Should().Throw<ArgumentException>()
            .WithParameterName("key");
        actEmpty.Should().Throw<ArgumentException>()
            .WithParameterName("key");
        actWhitespace.Should().Throw<ArgumentException>()
            .WithParameterName("key");
    }

    [Fact]
    public void WithLogger_SetsLogger()
    {
        // Arrange
        var builder = new InstallationBuilder();
        var logger = Substitute.For<ILogger>();

        // Act
        var result = builder.WithLogger(logger);

        // Assert
        result.Should().Be(builder, "builder should return itself for fluent chaining");
    }

    [Fact]
    public void WithLogger_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new InstallationBuilder();

        // Act
        Action act = () => builder.WithLogger(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void WithProgress_SetsProgressReporter()
    {
        // Arrange
        var builder = new InstallationBuilder();
        var progress = new Progress<InstallationProgress>();

        // Act
        var result = builder.WithProgress(progress);

        // Assert
        result.Should().Be(builder, "builder should return itself for fluent chaining");
    }

    [Fact]
    public void WithCancellationToken_SetsToken()
    {
        // Arrange
        var builder = new InstallationBuilder();
        var cts = new CancellationTokenSource();

        // Act
        var result = builder.WithCancellationToken(cts.Token);

        // Assert
        result.Should().Be(builder, "builder should return itself for fluent chaining");
    }

    [Fact]
    public void WithOptions_ConfiguresOptions()
    {
        // Arrange
        var builder = new InstallationBuilder();
        var logger = Substitute.For<ILogger>();

        // Act
        var result = builder
            .WithLogger(logger)
            .WithStep(new MockInstallationStep())
            .WithOptions(opts =>
            {
                opts.RollbackOnFailure = false;
                opts.ValidateBeforeInstall = false;
                opts.Timeout = TimeSpan.FromMinutes(60);
            });

        // Assert
        result.Should().Be(builder, "builder should return itself for fluent chaining");
    }

    [Fact]
    public void WithOptions_WithNullConfigure_ThrowsArgumentNullException()
    {
        // Arrange
        var builder = new InstallationBuilder();

        // Act
        Action act = () => builder.WithOptions(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("configure");
    }

    [Fact]
    public void WithInstallationPath_SetsPath()
    {
        // Arrange
        var builder = new InstallationBuilder();

        // Act
        var result = builder.WithInstallationPath("/opt/myapp");

        // Assert
        result.Should().Be(builder, "builder should return itself for fluent chaining");
    }

    [Fact]
    public void WithInstallationPath_WithNullOrEmptyPath_ThrowsArgumentException()
    {
        // Arrange
        var builder = new InstallationBuilder();

        // Act
        Action actNull = () => builder.WithInstallationPath(null!);
        Action actEmpty = () => builder.WithInstallationPath(string.Empty);
        Action actWhitespace = () => builder.WithInstallationPath("  ");

        // Assert
        actNull.Should().Throw<ArgumentException>()
            .WithParameterName("path");
        actEmpty.Should().Throw<ArgumentException>()
            .WithParameterName("path");
        actWhitespace.Should().Throw<ArgumentException>()
            .WithParameterName("path");
    }

    [Fact]
    public void Build_WithoutLogger_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new InstallationBuilder();
        builder.WithStep(new MockInstallationStep());

        // Act
        Action act = () => builder.Build();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Logger is required*");
    }

    [Fact]
    public void Build_WithoutSteps_ThrowsInvalidOperationException()
    {
        // Arrange
        var builder = new InstallationBuilder();
        var logger = Substitute.For<ILogger>();
        builder.WithLogger(logger);

        // Act
        Action act = () => builder.Build();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*at least one installation step is required*");
    }

    [Fact]
    public void Build_ReturnsInstallation()
    {
        // Arrange
        var builder = new InstallationBuilder();
        var logger = Substitute.For<ILogger>();
        var step1 = new MockInstallationStep { Name = "Step1" };
        var step2 = new MockInstallationStep { Name = "Step2" };

        builder
            .WithLogger(logger)
            .WithStep(step1)
            .WithStep(step2);

        // Act
        var installation = builder.Build();

        // Assert
        installation.Should().NotBeNull();
        installation.Should().BeAssignableTo<IInstallation>();
    }

    [Fact]
    public async Task Build_CopiesPropertiesToContext()
    {
        // Arrange
        var builder = new InstallationBuilder();
        var logger = Substitute.For<ILogger>();
        var step = new MockInstallationStep();

        builder
            .WithLogger(logger)
            .WithStep(step)
            .WithProperty("InstallPath", "/opt/app")
            .WithProperty("Version", "1.0.0")
            .WithProperty("Debug", true);

        // Act
        var installation = builder.Build();
        await installation.InstallAsync();

        // Assert - Check that properties were accessible during execution
        step.ExecuteCalled.Should().BeTrue("step should have executed with properties in context");
    }

    [Fact]
    public async Task Build_EndToEnd_BuilderToExecutorIntegration()
    {
        // Arrange
        var logger = Substitute.For<ILogger>();
        var progressReports = new List<InstallationProgress>();
        var progress = new Progress<InstallationProgress>(p => progressReports.Add(p));

        var step1 = new MockInstallationStep
        {
            Name = "ValidateEnvironment",
            ExecuteShouldSucceed = true
        };
        var step2 = new MockInstallationStep
        {
            Name = "CopyFiles",
            ExecuteShouldSucceed = true
        };
        var step3 = new MockInstallationStep
        {
            Name = "ConfigureService",
            ExecuteShouldSucceed = true
        };

        var builder = new InstallationBuilder();
        builder
            .WithLogger(logger)
            .WithProgress(progress)
            .WithProperty("TargetPath", "/opt/myapp")
            .WithProperty("ServiceName", "MyService")
            .WithStep(step1)
            .WithStep(step2)
            .WithStep(step3);

        // Act
        var installation = builder.Build();
        var result = await installation.InstallAsync();

        // Assert
        result.Success.Should().BeTrue();

        // Verify all steps executed
        step1.ExecuteCalled.Should().BeTrue();
        step2.ExecuteCalled.Should().BeTrue();
        step3.ExecuteCalled.Should().BeTrue();

        // Verify no rollbacks
        step1.RollbackCalled.Should().BeFalse();
        step2.RollbackCalled.Should().BeFalse();
        step3.RollbackCalled.Should().BeFalse();

        // Verify logging occurred
        logger.ReceivedCalls().Should().NotBeEmpty();
    }

    [Fact]
    public void Builder_CanBeUsed_MultipleTimes()
    {
        // Arrange
        var builder = new InstallationBuilder();
        var logger = Substitute.For<ILogger>();

        builder
            .WithLogger(logger)
            .WithStep(new MockInstallationStep { Name = "Step1" })
            .WithStep(new MockInstallationStep { Name = "Step2" });

        // Act
        var installation1 = builder.Build();
        var installation2 = builder.Build();

        // Assert
        installation1.Should().NotBeNull();
        installation2.Should().NotBeNull();
        installation1.Should().NotBeSameAs(installation2, "each Build() should create a new installation instance");
    }

    [Fact]
    public void WithProgress_WithNullProgress_IsAllowed()
    {
        // Arrange
        var builder = new InstallationBuilder();

        // Act
        var result = builder.WithProgress(null!);

        // Assert
        result.Should().Be(builder);
    }

    [Fact]
    public void Build_WithAllOptions_CreatesCompleteConfiguration()
    {
        // Arrange
        var logger = Substitute.For<ILogger>();
        var progress = new Progress<InstallationProgress>();
        var cts = new CancellationTokenSource();

        var builder = new InstallationBuilder();
        builder
            .WithLogger(logger)
            .WithProgress(progress)
            .WithCancellationToken(cts.Token)
            .WithProperty("Prop1", "Value1")
            .WithStep(new MockInstallationStep());

        // Act
        var installation = builder.Build();

        // Assert
        installation.Should().NotBeNull();
        installation.Should().BeAssignableTo<IInstallation>();
    }

    [Fact]
    public async Task Build_IntegrationWithFailure_TriggersRollback()
    {
        // Arrange
        var logger = Substitute.For<ILogger>();

        var step1 = new MockInstallationStep
        {
            Name = "Step1",
            ExecuteShouldSucceed = true
        };
        var step2 = new MockInstallationStep
        {
            Name = "Step2",
            ExecuteShouldSucceed = false // This will fail
        };

        var builder = new InstallationBuilder();
        builder
            .WithLogger(logger)
            .WithStep(step1)
            .WithStep(step2);

        // Act
        var installation = builder.Build();
        var result = await installation.InstallAsync();

        // Assert
        result.Success.Should().BeFalse();

        // Verify rollback was triggered
        step1.RollbackCalled.Should().BeTrue("step1 should be rolled back");
        step2.RollbackCalled.Should().BeTrue("step2 should be rolled back");
    }

    [Fact]
    public async Task Build_WithRollbackDisabled_DoesNotRollbackOnFailure()
    {
        // Arrange
        var logger = Substitute.For<ILogger>();

        var step1 = new MockInstallationStep
        {
            Name = "Step1",
            ExecuteShouldSucceed = true
        };
        var step2 = new MockInstallationStep
        {
            Name = "Step2",
            ExecuteShouldSucceed = false
        };

        var builder = new InstallationBuilder();
        builder
            .WithLogger(logger)
            .WithOptions(opts => opts.RollbackOnFailure = false)
            .WithStep(step1)
            .WithStep(step2);

        // Act
        var installation = builder.Build();
        var result = await installation.InstallAsync();

        // Assert
        result.Success.Should().BeFalse();
        step1.RollbackCalled.Should().BeFalse("rollback should be disabled");
        step2.RollbackCalled.Should().BeFalse("rollback should be disabled");
    }
}
