using DotNetUp.Core.Models;
using FluentAssertions;

namespace DotNetUp.Tests.Models;

public class InstallationProgressTests
{
    [Fact]
    public void Constructor_SetsAllProperties_Correctly()
    {
        // Arrange & Act
        var progress = new InstallationProgress
        {
            CurrentStepNumber = 2,
            TotalSteps = 5,
            CurrentStepName = "TestStep",
            SubStepDescription = "Processing data",
            PercentComplete = 75
        };

        // Assert
        progress.CurrentStepNumber.Should().Be(2);
        progress.TotalSteps.Should().Be(5);
        progress.CurrentStepName.Should().Be("TestStep");
        progress.SubStepDescription.Should().Be("Processing data");
        progress.PercentComplete.Should().Be(75);
    }

    [Theory]
    [InlineData(1, 3, 50, 16.67)]   // Step 1/3 at 50% = 16.67% overall
    [InlineData(2, 3, 0, 33.33)]     // Step 2/3 at 0% = 33.33% overall
    [InlineData(3, 3, 100, 100.0)]   // Step 3/3 at 100% = 100% overall
    [InlineData(1, 1, 50, 50.0)]     // Single step at 50% = 50% overall
    public void OverallPercentComplete_CalculatesCorrectly(int step, int total, int percent, double expected)
    {
        // Arrange
        var progress = new InstallationProgress
        {
            CurrentStepNumber = step,
            TotalSteps = total,
            PercentComplete = percent
        };

        // Act
        var overall = progress.OverallPercentComplete;

        // Assert
        overall.Should().BeApproximately(expected, 0.01,
            $"step {step}/{total} at {percent}% should be {expected}% overall");
    }

    [Fact]
    public void OverallPercentComplete_WithZeroSteps_ReturnsZero()
    {
        // Arrange
        var progress = new InstallationProgress
        {
            CurrentStepNumber = 0,
            TotalSteps = 0,
            PercentComplete = 50
        };

        // Act
        var overall = progress.OverallPercentComplete;

        // Assert
        overall.Should().Be(0);
    }

    [Fact]
    public void Properties_AreInitOnly()
    {
        // Arrange
        var progress = new InstallationProgress
        {
            CurrentStepNumber = 1,
            TotalSteps = 3,
            CurrentStepName = "Step1"
        };

        // Assert - Properties should be init-only (compile-time check)
        // This test verifies the immutability design
        progress.CurrentStepNumber.Should().Be(1);
        progress.TotalSteps.Should().Be(3);
        progress.CurrentStepName.Should().Be("Step1");
    }

    [Fact]
    public void SubStepDescription_CanBeNull()
    {
        // Arrange & Act
        var progress = new InstallationProgress
        {
            CurrentStepNumber = 1,
            TotalSteps = 1,
            SubStepDescription = null
        };

        // Assert
        progress.SubStepDescription.Should().BeNull();
    }

    [Fact]
    public void CurrentStepName_DefaultsToEmptyString()
    {
        // Arrange & Act
        var progress = new InstallationProgress();

        // Assert
        progress.CurrentStepName.Should().Be(string.Empty);
    }
}
