using DotNetUp.Core.Models;
using DotNetUp.Tests.Fixtures;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotNetUp.Tests.Models;

public class InstallationContextTests
{
    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act
        Action act = () => new InstallationContext(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithValidLogger_InitializesProperties()
    {
        // Arrange
        var logger = Substitute.For<ILogger>();

        // Act
        var context = new InstallationContext(logger);

        // Assert
        context.Logger.Should().Be(logger);
        context.Properties.Should().NotBeNull().And.BeEmpty();
        context.Progress.Should().BeNull();
        context.CancellationToken.Should().Be(CancellationToken.None);
    }

    [Fact]
    public void Properties_CanBeAddedAndRetrieved()
    {
        // Arrange
        var context = TestInstallationContext.Create();

        // Act
        context.Properties["Key1"] = "Value1";
        context.Properties["Key2"] = 42;
        context.Properties["Key3"] = new { Name = "Test" };

        // Assert
        context.Properties.Should().HaveCount(3);
        context.Properties["Key1"].Should().Be("Value1");
        context.Properties["Key2"].Should().Be(42);
        context.Properties["Key3"].Should().BeEquivalentTo(new { Name = "Test" });
    }

    [Fact]
    public async Task SetCurrentStep_UpdatesInternalState()
    {
        // Arrange
        var context = TestInstallationContext.Create();

        // Act
        context.SetCurrentStep(2, 5, "TestStep");

        // Assert - We can verify this by checking the progress reporting
        var progressReports = new List<InstallationProgress>();
        var progress = new Progress<InstallationProgress>(p => progressReports.Add(p));

        var contextWithProgress = new InstallationContext(
            Substitute.For<ILogger>(),
            progress);
        contextWithProgress.SetCurrentStep(2, 5, "TestStep");
        contextWithProgress.ReportStepProgress("Working", 50);

        await Task.Yield();

        progressReports.Should().HaveCount(1);
        progressReports[0].CurrentStepNumber.Should().Be(2);
        progressReports[0].TotalSteps.Should().Be(5);
        progressReports[0].CurrentStepName.Should().Be("TestStep");
    }

    [Fact]
    public async Task ReportStepProgress_CreatesCorrectInstallationProgress()
    {
        // Arrange
        var progressReports = new List<InstallationProgress>();
        var progress = new Progress<InstallationProgress>(p => progressReports.Add(p));
        var context = TestInstallationContext.Create(progress: progress);

        context.SetCurrentStep(3, 10, "DeployStep");

        // Act
        context.ReportStepProgress("Copying files", 75);

        await Task.Yield();

        // Assert
        progressReports.Should().HaveCount(1);
        var report = progressReports[0];
        report.CurrentStepNumber.Should().Be(3);
        report.TotalSteps.Should().Be(10);
        report.CurrentStepName.Should().Be("DeployStep");
        report.SubStepDescription.Should().Be("Copying files");
        report.PercentComplete.Should().Be(75);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(-50)]
    [InlineData(150)]
    public void ReportStepProgress_WithInvalidPercentage_ThrowsArgumentOutOfRangeException(int invalidPercent)
    {
        // Arrange
        var context = TestInstallationContext.Create();
        context.SetCurrentStep(1, 1, "Test");

        // Act
        Action act = () => context.ReportStepProgress("Test", invalidPercent);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("percentComplete")
            .WithMessage("*must be between 0 and 100*");
    }

    [Fact]
    public async Task ReportStepProgress_CallsProgressReporter()
    {
        // Arrange
        var progressReports = new List<InstallationProgress>();
        var progress = new Progress<InstallationProgress>(p => progressReports.Add(p));
        var context = TestInstallationContext.Create(progress: progress);

        context.SetCurrentStep(1, 3, "Step1");

        // Act
        context.ReportStepProgress("Substep 1", 25);
        context.ReportStepProgress("Substep 2", 50);
        context.ReportStepProgress("Substep 3", 100);

        await Task.Yield();

        // Assert
        progressReports.Should().HaveCount(3);
        progressReports[0].PercentComplete.Should().Be(25);
        progressReports[1].PercentComplete.Should().Be(50);
        progressReports[2].PercentComplete.Should().Be(100);
    }

    [Fact]
    public void ReportStepProgress_LogsStructuredInformation()
    {
        // Arrange
        var mockLogger = Substitute.For<ILogger>();
        var context = TestInstallationContext.Create(logger: mockLogger);

        context.SetCurrentStep(2, 4, "DatabaseMigration");

        // Act
        context.ReportStepProgress("Running migration scripts", 60);

        // Assert - Verify that logging occurred
        mockLogger.ReceivedCalls().Should().NotBeEmpty("because progress should be logged");
    }

    [Fact]
    public void ReportStepProgress_WithNullProgress_DoesNotThrow()
    {
        // Arrange
        var context = TestInstallationContext.Create(progress: null);
        context.SetCurrentStep(1, 1, "Test");

        // Act
        Action act = () => context.ReportStepProgress("Working", 50);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithCancellationToken_StoresToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var logger = Substitute.For<ILogger>();

        // Act
        var context = new InstallationContext(logger, null, cts.Token);

        // Assert
        context.CancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task ReportStepProgress_CalculatesOverallProgress_Correctly()
    {
        // Arrange
        var progressReports = new List<InstallationProgress>();
        var progress = new Progress<InstallationProgress>(p => progressReports.Add(p));
        var context = TestInstallationContext.Create(progress: progress);

        context.SetCurrentStep(2, 4, "Step2");

        // Act
        context.ReportStepProgress("Processing", 50);

        await Task.Yield();

        // Assert
        progressReports.Should().HaveCount(1, "because one progress report should have been generated");
        var report = progressReports[0];
        // Step 2 of 4 at 50% = (1 * 100 + 50) / 4 = 37.5%
        report.OverallPercentComplete.Should().BeApproximately(37.5, 0.01);
    }
}
