using DotNetUp.Core.Models;
using FluentAssertions;

namespace DotNetUp.Tests.Models;

public class InstallationOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new InstallationOptions();

        // Assert
        options.RollbackOnFailure.Should().BeTrue("default should be true");
        options.ValidateBeforeInstall.Should().BeTrue("default should be true");
        options.CreateBackup.Should().BeTrue("default should be true");
        options.Timeout.Should().Be(TimeSpan.FromMinutes(30), "default timeout should be 30 minutes");
        options.RequireAdministrator.Should().BeFalse("default should be false");
    }

    [Fact]
    public void RollbackOnFailure_CanBeSet()
    {
        // Arrange
        var options = new InstallationOptions();

        // Act
        options.RollbackOnFailure = false;

        // Assert
        options.RollbackOnFailure.Should().BeFalse();
    }

    [Fact]
    public void ValidateBeforeInstall_CanBeSet()
    {
        // Arrange
        var options = new InstallationOptions();

        // Act
        options.ValidateBeforeInstall = false;

        // Assert
        options.ValidateBeforeInstall.Should().BeFalse();
    }

    [Fact]
    public void CreateBackup_CanBeSet()
    {
        // Arrange
        var options = new InstallationOptions();

        // Act
        options.CreateBackup = false;

        // Assert
        options.CreateBackup.Should().BeFalse();
    }

    [Fact]
    public void Timeout_CanBeSet()
    {
        // Arrange
        var options = new InstallationOptions();

        // Act
        options.Timeout = TimeSpan.FromHours(1);

        // Assert
        options.Timeout.Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void RequireAdministrator_CanBeSet()
    {
        // Arrange
        var options = new InstallationOptions();

        // Act
        options.RequireAdministrator = true;

        // Assert
        options.RequireAdministrator.Should().BeTrue();
    }

    [Fact]
    public void AllProperties_CanBeSetTogether()
    {
        // Arrange & Act
        var options = new InstallationOptions
        {
            RollbackOnFailure = false,
            ValidateBeforeInstall = false,
            CreateBackup = false,
            Timeout = TimeSpan.FromMinutes(60),
            RequireAdministrator = true
        };

        // Assert
        options.RollbackOnFailure.Should().BeFalse();
        options.ValidateBeforeInstall.Should().BeFalse();
        options.CreateBackup.Should().BeFalse();
        options.Timeout.Should().Be(TimeSpan.FromMinutes(60));
        options.RequireAdministrator.Should().BeTrue();
    }
}
