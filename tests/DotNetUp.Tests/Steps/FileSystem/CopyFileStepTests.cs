using DotNetUp.Steps.FileSystem;
using DotNetUp.Tests.Fixtures;
using FluentAssertions;

namespace DotNetUp.Tests.Steps.FileSystem;

public class CopyFileStepTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _sourceFile;
    private readonly string _destinationFile;

    public CopyFileStepTests()
    {
        // Create a unique test directory for each test
        _testDir = Path.Combine(Path.GetTempPath(), $"DotNetUpTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);

        _sourceFile = Path.Combine(_testDir, "source.txt");
        _destinationFile = Path.Combine(_testDir, "destination.txt");
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testDir))
        {
            try
            {
                Directory.Delete(_testDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    // ============================================
    // Constructor Tests
    // ============================================

    [Fact]
    public void Constructor_WithValidPaths_SetsProperties()
    {
        // Arrange & Act
        var step = new CopyFileStep("source.txt", "dest.txt", overwrite: true);

        // Assert
        step.SourcePath.Should().Be("source.txt");
        step.DestinationPath.Should().Be("dest.txt");
        step.Overwrite.Should().BeTrue();
        step.Name.Should().Be("CopyFile");
        step.Description.Should().Contain("source.txt").And.Contain("dest.txt");
    }

    [Fact]
    public void Constructor_WithNullSourcePath_ThrowsArgumentException()
    {
        // Act
        Action act = () => new CopyFileStep(null!, "dest.txt");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("sourcePath");
    }

    [Fact]
    public void Constructor_WithEmptySourcePath_ThrowsArgumentException()
    {
        // Act
        Action act = () => new CopyFileStep("", "dest.txt");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("sourcePath");
    }

    [Fact]
    public void Constructor_WithWhitespaceSourcePath_ThrowsArgumentException()
    {
        // Act
        Action act = () => new CopyFileStep("   ", "dest.txt");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("sourcePath");
    }

    [Fact]
    public void Constructor_WithNullDestinationPath_ThrowsArgumentException()
    {
        // Act
        Action act = () => new CopyFileStep("source.txt", null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("destinationPath");
    }

    [Fact]
    public void Constructor_WithEmptyDestinationPath_ThrowsArgumentException()
    {
        // Act
        Action act = () => new CopyFileStep("source.txt", "");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("destinationPath");
    }

    // ============================================
    // Validation Tests - Success Scenarios
    // ============================================

    [Fact]
    public async Task ValidateAsync_WhenSourceExistsAndDestinationDoesNotExist_ReturnsSuccess()
    {
        // Arrange
        File.WriteAllText(_sourceFile, "test content");
        var step = new CopyFileStep(_sourceFile, _destinationFile);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ValidateAsync(context);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WhenSourceExistsAndOverwriteEnabled_ReturnsSuccess()
    {
        // Arrange
        File.WriteAllText(_sourceFile, "source content");
        File.WriteAllText(_destinationFile, "existing content");
        var step = new CopyFileStep(_sourceFile, _destinationFile, overwrite: true);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ValidateAsync(context);

        // Assert
        result.Success.Should().BeTrue();
    }

    // ============================================
    // Validation Tests - Failure Scenarios
    // ============================================

    [Fact]
    public async Task ValidateAsync_WhenSourceDoesNotExist_ReturnsFailure()
    {
        // Arrange
        var nonExistentSource = Path.Combine(_testDir, "nonexistent.txt");
        var step = new CopyFileStep(nonExistentSource, _destinationFile);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ValidateAsync(context);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Source file does not exist");
    }

    [Fact]
    public async Task ValidateAsync_WhenDestinationDirectoryDoesNotExist_ReturnsFailure()
    {
        // Arrange
        File.WriteAllText(_sourceFile, "test content");
        var nonExistentDir = Path.Combine(_testDir, "nonexistent", "dest.txt");
        var step = new CopyFileStep(_sourceFile, nonExistentDir);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ValidateAsync(context);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Destination directory does not exist");
    }

    [Fact]
    public async Task ValidateAsync_WhenDestinationExistsAndOverwriteDisabled_ReturnsFailure()
    {
        // Arrange
        File.WriteAllText(_sourceFile, "source content");
        File.WriteAllText(_destinationFile, "existing content");
        var step = new CopyFileStep(_sourceFile, _destinationFile, overwrite: false);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ValidateAsync(context);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already exists");
        result.Message.Should().Contain("overwrite is not enabled");
    }

    // ============================================
    // Execution Tests - Success Scenarios
    // ============================================

    [Fact]
    public async Task ExecuteAsync_WhenDestinationDoesNotExist_CopiesFile()
    {
        // Arrange
        var sourceContent = "test content";
        File.WriteAllText(_sourceFile, sourceContent);
        var step = new CopyFileStep(_sourceFile, _destinationFile);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        File.Exists(_destinationFile).Should().BeTrue();
        File.ReadAllText(_destinationFile).Should().Be(sourceContent);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOverwriteEnabled_CopiesFileAndCreatesBackup()
    {
        // Arrange
        var sourceContent = "new content";
        var existingContent = "existing content";
        File.WriteAllText(_sourceFile, sourceContent);
        File.WriteAllText(_destinationFile, existingContent);

        var step = new CopyFileStep(_sourceFile, _destinationFile, overwrite: true);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        File.ReadAllText(_destinationFile).Should().Be(sourceContent);

        // Backup should exist (cleanup happens in rollback)
        var backupFiles = Directory.GetFiles(_testDir, "*.backup_*");
        backupFiles.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_PreservesFileContent_ExactCopy()
    {
        // Arrange
        var sourceContent = "Line 1\nLine 2\nLine 3\nSpecial chars: @#$%^&*()";
        File.WriteAllText(_sourceFile, sourceContent);
        var step = new CopyFileStep(_sourceFile, _destinationFile);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        var copiedContent = File.ReadAllText(_destinationFile);
        copiedContent.Should().Be(sourceContent);
    }

    // ============================================
    // Execution Tests - Failure Scenarios
    // ============================================

    [Fact]
    public async Task ExecuteAsync_WhenSourceDoesNotExist_ReturnsFailure()
    {
        // Arrange
        var nonExistentSource = Path.Combine(_testDir, "nonexistent.txt");
        var step = new CopyFileStep(nonExistentSource, _destinationFile);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeFalse();
        result.Exception.Should().NotBeNull();
    }

    // ============================================
    // Rollback Tests - Success Scenarios
    // ============================================

    [Fact]
    public async Task RollbackAsync_WhenDestinationWasCreated_DeletesDestination()
    {
        // Arrange
        File.WriteAllText(_sourceFile, "test content");
        var step = new CopyFileStep(_sourceFile, _destinationFile);
        var context = TestInstallationContext.Create();

        await step.ExecuteAsync(context);
        File.Exists(_destinationFile).Should().BeTrue("destination should exist after execute");

        // Act
        var result = await step.RollbackAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        File.Exists(_destinationFile).Should().BeFalse("destination should be deleted after rollback");
    }

    [Fact]
    public async Task RollbackAsync_WhenDestinationWasOverwritten_RestoresBackup()
    {
        // Arrange
        var sourceContent = "new content";
        var originalContent = "original content";
        File.WriteAllText(_sourceFile, sourceContent);
        File.WriteAllText(_destinationFile, originalContent);

        var step = new CopyFileStep(_sourceFile, _destinationFile, overwrite: true);
        var context = TestInstallationContext.Create();

        await step.ExecuteAsync(context);
        File.ReadAllText(_destinationFile).Should().Be(sourceContent, "destination should have new content after execute");

        // Act
        var result = await step.RollbackAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        File.ReadAllText(_destinationFile).Should().Be(originalContent, "destination should be restored to original content after rollback");

        // Backup file should be cleaned up
        var backupFiles = Directory.GetFiles(_testDir, "*.backup_*");
        backupFiles.Should().BeEmpty("backup files should be deleted after rollback");
    }

    [Fact]
    public async Task RollbackAsync_BeforeExecute_DoesNothing()
    {
        // Arrange
        File.WriteAllText(_sourceFile, "test content");
        var step = new CopyFileStep(_sourceFile, _destinationFile);
        var context = TestInstallationContext.Create();

        // Act - Call rollback without execute
        var result = await step.RollbackAsync(context);

        // Assert
        result.Success.Should().BeTrue("rollback should succeed even if nothing was executed");
    }

    // ============================================
    // Rollback Tests - Best-Effort Failure Scenarios
    // ============================================

    [Fact]
    public async Task RollbackAsync_WhenDestinationIsLocked_ReturnsFailure()
    {
        // Skip on non-Windows platforms where file locking works differently
        if (!OperatingSystem.IsWindows())
        {
            // On Linux/macOS, you can delete a file while it's open
            return;
        }

        // Arrange
        File.WriteAllText(_sourceFile, "test content");
        var step = new CopyFileStep(_sourceFile, _destinationFile);
        var context = TestInstallationContext.Create();

        await step.ExecuteAsync(context);

        // Lock the destination file
        using var fileStream = File.Open(_destinationFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        // Act
        var result = await step.RollbackAsync(context);

        // Assert
        result.Success.Should().BeFalse("rollback should fail when file is locked");
        result.Message.Should().Contain("Rollback failed");
    }

    // ============================================
    // Integration Tests
    // ============================================

    [Fact]
    public async Task CompleteWorkflow_ValidateExecuteRollback_WorksCorrectly()
    {
        // Arrange
        var sourceContent = "test content";
        File.WriteAllText(_sourceFile, sourceContent);
        var step = new CopyFileStep(_sourceFile, _destinationFile);
        var context = TestInstallationContext.Create();

        // Act & Assert - Validate
        var validateResult = await step.ValidateAsync(context);
        validateResult.Success.Should().BeTrue();

        // Act & Assert - Execute
        var executeResult = await step.ExecuteAsync(context);
        executeResult.Success.Should().BeTrue();
        File.Exists(_destinationFile).Should().BeTrue();
        File.ReadAllText(_destinationFile).Should().Be(sourceContent);

        // Act & Assert - Rollback
        var rollbackResult = await step.RollbackAsync(context);
        rollbackResult.Success.Should().BeTrue();
        File.Exists(_destinationFile).Should().BeFalse();
    }

    [Fact]
    public async Task CompleteWorkflow_WithOverwrite_ValidateExecuteRollback()
    {
        // Arrange
        var sourceContent = "new content";
        var originalContent = "original content";
        File.WriteAllText(_sourceFile, sourceContent);
        File.WriteAllText(_destinationFile, originalContent);

        var step = new CopyFileStep(_sourceFile, _destinationFile, overwrite: true);
        var context = TestInstallationContext.Create();

        // Act & Assert - Validate
        var validateResult = await step.ValidateAsync(context);
        validateResult.Success.Should().BeTrue();

        // Act & Assert - Execute
        var executeResult = await step.ExecuteAsync(context);
        executeResult.Success.Should().BeTrue();
        File.ReadAllText(_destinationFile).Should().Be(sourceContent);

        // Act & Assert - Rollback
        var rollbackResult = await step.RollbackAsync(context);
        rollbackResult.Success.Should().BeTrue();
        File.ReadAllText(_destinationFile).Should().Be(originalContent);
    }

    // ============================================
    // Edge Case Tests
    // ============================================

    [Fact]
    public async Task ExecuteAsync_WithLongFilePath_WorksCorrectly()
    {
        // Arrange
        var longFileName = new string('a', 100) + ".txt";
        var longSource = Path.Combine(_testDir, longFileName);
        var longDest = Path.Combine(_testDir, "dest_" + longFileName);

        File.WriteAllText(longSource, "test content");
        var step = new CopyFileStep(longSource, longDest);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        File.Exists(longDest).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithSpecialCharactersInFileName_WorksCorrectly()
    {
        // Arrange
        var specialFileName = "test_file-2024(1).txt";
        var specialSource = Path.Combine(_testDir, specialFileName);
        var specialDest = Path.Combine(_testDir, "dest_" + specialFileName);

        File.WriteAllText(specialSource, "test content");
        var step = new CopyFileStep(specialSource, specialDest);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        File.Exists(specialDest).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyFile_WorksCorrectly()
    {
        // Arrange
        File.WriteAllText(_sourceFile, string.Empty);
        var step = new CopyFileStep(_sourceFile, _destinationFile);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        File.Exists(_destinationFile).Should().BeTrue();
        File.ReadAllText(_destinationFile).Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithLargeFile_WorksCorrectly()
    {
        // Arrange
        var largeContent = new string('x', 1024 * 1024); // 1MB
        File.WriteAllText(_sourceFile, largeContent);
        var step = new CopyFileStep(_sourceFile, _destinationFile);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        File.Exists(_destinationFile).Should().BeTrue();
        var copiedContent = File.ReadAllText(_destinationFile);
        copiedContent.Length.Should().Be(largeContent.Length);
    }
}
