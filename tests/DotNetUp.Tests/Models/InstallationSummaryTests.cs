using DotNetUp.Core.Models;
using FluentAssertions;

namespace DotNetUp.Tests.Models;

public class InstallationSummaryTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var summary = new InstallationSummary();

        // Assert
        summary.Success.Should().BeFalse("default should be false");
        summary.Message.Should().BeEmpty("default should be empty");
        summary.Exception.Should().BeNull("default should be null");
        summary.StepResults.Should().BeEmpty("default should be empty dictionary");
        summary.Duration.Should().Be(TimeSpan.Zero, "default should be zero");
        summary.CompletedSteps.Should().Be(0, "default should be 0");
        summary.FailedStep.Should().BeNull("default should be null");
    }

    [Fact]
    public void SuccessSummary_CreatesCorrectResult()
    {
        // Arrange
        var stepResults = new Dictionary<string, InstallationStepResult>
        {
            ["Step1"] = InstallationStepResult.SuccessResult("Step 1 completed"),
            ["Step2"] = InstallationStepResult.SuccessResult("Step 2 completed")
        };
        var duration = TimeSpan.FromSeconds(10);

        // Act
        var summary = InstallationSummary.SuccessSummary(
            "Installation completed successfully",
            stepResults,
            duration);

        // Assert
        summary.Success.Should().BeTrue();
        summary.Message.Should().Be("Installation completed successfully");
        summary.Exception.Should().BeNull();
        summary.StepResults.Should().HaveCount(2);
        summary.Duration.Should().Be(duration);
        summary.CompletedSteps.Should().Be(2);
        summary.FailedStep.Should().BeNull();
    }

    [Fact]
    public void FailureSummary_CreatesCorrectResult()
    {
        // Arrange
        var stepResults = new Dictionary<string, InstallationStepResult>
        {
            ["Step1"] = InstallationStepResult.SuccessResult("Step 1 completed"),
            ["Step2"] = InstallationStepResult.FailureResult("Step 2 failed")
        };
        var duration = TimeSpan.FromSeconds(5);
        var exception = new InvalidOperationException("Test error");

        // Act
        var summary = InstallationSummary.FailureSummary(
            "Installation failed at Step2",
            "Step2",
            stepResults,
            duration,
            exception);

        // Assert
        summary.Success.Should().BeFalse();
        summary.Message.Should().Be("Installation failed at Step2");
        summary.Exception.Should().Be(exception);
        summary.StepResults.Should().HaveCount(2);
        summary.Duration.Should().Be(duration);
        summary.CompletedSteps.Should().Be(1, "only Step1 succeeded");
        summary.FailedStep.Should().Be("Step2");
    }

    [Fact]
    public void FailureSummary_WithNoException_WorksCorrectly()
    {
        // Arrange
        var stepResults = new Dictionary<string, InstallationStepResult>
        {
            ["Step1"] = InstallationStepResult.FailureResult("Step 1 failed")
        };

        // Act
        var summary = InstallationSummary.FailureSummary(
            "Installation failed",
            "Step1",
            stepResults,
            TimeSpan.FromSeconds(1));

        // Assert
        summary.Success.Should().BeFalse();
        summary.Exception.Should().BeNull();
        summary.FailedStep.Should().Be("Step1");
    }

    [Fact]
    public void FailureSummary_CountsOnlySuccessfulSteps()
    {
        // Arrange
        var stepResults = new Dictionary<string, InstallationStepResult>
        {
            ["Step1"] = InstallationStepResult.SuccessResult("Success"),
            ["Step2"] = InstallationStepResult.SuccessResult("Success"),
            ["Step3"] = InstallationStepResult.FailureResult("Failed"),
            ["Step4"] = InstallationStepResult.SuccessResult("Success")
        };

        // Act
        var summary = InstallationSummary.FailureSummary(
            "Failed",
            "Step3",
            stepResults,
            TimeSpan.FromSeconds(1));

        // Assert
        summary.CompletedSteps.Should().Be(3, "3 steps succeeded, 1 failed");
    }

    [Fact]
    public void SuccessSummary_WithEmptyStepResults_ReturnsZeroCompletedSteps()
    {
        // Arrange
        var stepResults = new Dictionary<string, InstallationStepResult>();

        // Act
        var summary = InstallationSummary.SuccessSummary(
            "Empty installation",
            stepResults,
            TimeSpan.Zero);

        // Assert
        summary.CompletedSteps.Should().Be(0);
        summary.StepResults.Should().BeEmpty();
    }

    [Fact]
    public void StepResults_CanBeAccessed()
    {
        // Arrange
        var step1Result = InstallationStepResult.SuccessResult("Step 1 done");
        var stepResults = new Dictionary<string, InstallationStepResult>
        {
            ["Step1"] = step1Result
        };

        // Act
        var summary = InstallationSummary.SuccessSummary("Done", stepResults, TimeSpan.FromSeconds(1));

        // Assert
        summary.StepResults["Step1"].Should().Be(step1Result);
        summary.StepResults["Step1"].Success.Should().BeTrue();
        summary.StepResults["Step1"].Message.Should().Be("Step 1 done");
    }
}
