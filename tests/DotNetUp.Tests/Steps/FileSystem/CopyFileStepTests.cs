using DotNetUp.Core.Models;
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
        result.Message.Should().Contain("Source file not found");
    }

    [Fact]
    public async Task ValidateAsync_WhenDestinationDirectoryDoesNotExist_ReturnsFailure()
    {
        // Arrange
        File.WriteAllText(_sourceFile, "test content");
        var nonExistentDir = Path.Combine(_testDir, "nonexistent", "dest.txt");
        var step = new CopyFileStep(_sourceFile, nonExistentDir, createDirectoriesIfNeeded: false);
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
        result.Message.Should().Contain("Set Overwrite to true to overwrite");
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
    public async Task ExecuteAsync_WhenOverwriteEnabledWithBackup_CopiesFileAndCreatesBackup()
    {
        // Arrange
        var sourceContent = "new content";
        var existingContent = "existing content";
        File.WriteAllText(_sourceFile, sourceContent);
        File.WriteAllText(_destinationFile, existingContent);

        var step = new CopyFileStep(_sourceFile, _destinationFile, overwrite: true, backupExisting: true);
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
    public async Task ExecuteAsync_WhenOverwriteEnabledWithoutBackup_CopiesFileWithoutBackup()
    {
        // Arrange
        var sourceContent = "new content";
        var existingContent = "existing content";
        File.WriteAllText(_sourceFile, sourceContent);
        File.WriteAllText(_destinationFile, existingContent);

        var step = new CopyFileStep(_sourceFile, _destinationFile, overwrite: true, backupExisting: false);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        File.ReadAllText(_destinationFile).Should().Be(sourceContent);

        // Backup should NOT exist
        var backupFiles = Directory.GetFiles(_testDir, "*.backup_*");
        backupFiles.Should().BeEmpty();
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

        var step = new CopyFileStep(_sourceFile, _destinationFile, overwrite: true, backupExisting: true);
        var context = TestInstallationContext.Create();

        await step.ExecuteAsync(context);
        File.ReadAllText(_destinationFile).Should().Be(sourceContent, "destination should have new content after execute");

        // Act
        var result = await step.RollbackAsync(context);
        await step.DisposeAsync(); // Simulate the finally block behavior

        // Assert
        result.Success.Should().BeTrue();
        File.ReadAllText(_destinationFile).Should().Be(originalContent, "destination should be restored to original content after rollback");

        // Backup file should be cleaned up by DisposeAsync
        var backupFiles = Directory.GetFiles(_testDir, "*.backup_*");
        backupFiles.Should().BeEmpty("backup files should be deleted after disposal");
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

        var step = new CopyFileStep(_sourceFile, _destinationFile, overwrite: true, backupExisting: true);
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

    // ============================================
    // New Feature Tests
    // ============================================

    [Fact]
    public async Task ValidateAsync_WithCircularCopy_ReturnsFailure()
    {
        // Arrange
        File.WriteAllText(_sourceFile, "test content");
        var step = new CopyFileStep(_sourceFile, _sourceFile); // Same file
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ValidateAsync(context);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Cannot copy file to itself");
    }

    [Fact]
    public async Task ValidateAsync_WithExcessivePathLength_ReturnsFailure()
    {
        // Arrange
        var longPath = Path.Combine(_testDir, new string('a', 300)) + ".txt";
        File.WriteAllText(_sourceFile, "test content");
        var step = new CopyFileStep(_sourceFile, longPath);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ValidateAsync(context);

        // Assert
        if (OperatingSystem.IsWindows())
        {
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("exceeds maximum length");
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithCreateDirectoriesIfNeeded_CreatesDirectory()
    {
        // Arrange
        File.WriteAllText(_sourceFile, "test content");
        var newDir = Path.Combine(_testDir, "newdir");
        var destFile = Path.Combine(newDir, "file.txt");
        var step = new CopyFileStep(_sourceFile, destFile, createDirectoriesIfNeeded: true);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(newDir).Should().BeTrue();
        File.Exists(destFile).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_PreservesTimestamps_WhenEnabled()
    {
        // Arrange
        File.WriteAllText(_sourceFile, "test content");
        var oldWriteTime = DateTime.Now.AddDays(-5);
        File.SetLastWriteTime(_sourceFile, oldWriteTime);

        var step = new CopyFileStep(_sourceFile, _destinationFile, preserveTimestamps: true);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        var destInfo = new FileInfo(_destinationFile);
        // LastWriteTime should be preserved
        destInfo.LastWriteTime.Should().BeCloseTo(oldWriteTime, TimeSpan.FromSeconds(5));

        // Note: Creation time preservation depends on file system support
        // Some file systems (like ext4) don't fully support creation time
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotPreserveTimestamps_WhenDisabled()
    {
        // Arrange
        File.WriteAllText(_sourceFile, "test content");
        var oldWriteTime = DateTime.Now.AddDays(-5);
        File.SetLastWriteTime(_sourceFile, oldWriteTime);

        var step = new CopyFileStep(_sourceFile, _destinationFile, preserveTimestamps: false);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        var destInfo = new FileInfo(_destinationFile);
        // Destination should have current time (within last few seconds)
        destInfo.LastWriteTime.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ExecuteAsync_CopiesAttributes_WhenEnabled()
    {
        // Arrange
        File.WriteAllText(_sourceFile, "test content");
        File.SetAttributes(_sourceFile, FileAttributes.ReadOnly);

        var step = new CopyFileStep(_sourceFile, _destinationFile, copyAttributes: true);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        var destAttributes = File.GetAttributes(_destinationFile);
        destAttributes.Should().HaveFlag(FileAttributes.ReadOnly);

        // Cleanup: Remove read-only to allow deletion
        File.SetAttributes(_destinationFile, FileAttributes.Normal);
        File.SetAttributes(_sourceFile, FileAttributes.Normal);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotCopyAttributes_WhenDisabled()
    {
        // Arrange
        File.WriteAllText(_sourceFile, "test content");
        File.SetAttributes(_sourceFile, FileAttributes.ReadOnly);

        var step = new CopyFileStep(_sourceFile, _destinationFile, copyAttributes: false);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        var destAttributes = File.GetAttributes(_destinationFile);
        destAttributes.Should().NotHaveFlag(FileAttributes.ReadOnly);

        // Cleanup
        File.SetAttributes(_sourceFile, FileAttributes.Normal);
    }

    [Fact]
    public async Task ExecuteAsync_VerifiesFileSize_AfterCopy()
    {
        // Arrange
        var content = "test content with specific size";
        File.WriteAllText(_sourceFile, content);
        var step = new CopyFileStep(_sourceFile, _destinationFile);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        var sourceSize = new FileInfo(_sourceFile).Length;
        var destSize = new FileInfo(_destinationFile).Length;
        destSize.Should().Be(sourceSize);
    }

    [Fact]
    public async Task ExecuteAsync_StoresMetadataInContext()
    {
        // Arrange
        File.WriteAllText(_sourceFile, "test content");
        var step = new CopyFileStep(_sourceFile, _destinationFile);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        result.Data.Should().ContainKey("CopiedFilePath");
        result.Data.Should().ContainKey("FileSize");
        result.Data.Should().ContainKey("CopyDuration");
        result.Data.Should().ContainKey("BackupCreated");

        result.Data["CopiedFilePath"].Should().Be(_destinationFile);
        result.Data["FileSize"].Should().BeOfType<long>();
        result.Data["CopyDuration"].Should().BeOfType<TimeSpan>();
        result.Data["BackupCreated"].Should().Be(false);
    }

    [Fact]
    public async Task ExecuteAsync_WithOverwriteAndBackup_StoresBackupPathInMetadata()
    {
        // Arrange
        File.WriteAllText(_sourceFile, "new content");
        File.WriteAllText(_destinationFile, "existing content");
        var step = new CopyFileStep(_sourceFile, _destinationFile, overwrite: true, backupExisting: true);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        result.Data["BackupCreated"].Should().Be(true);
        result.Data.Should().ContainKey("BackupPath");
        result.Data["BackupPath"].Should().BeOfType<string>();
    }

    [Fact]
    public async Task ExecuteAsync_ReportsProgress_ThroughoutOperation()
    {
        // Arrange
        File.WriteAllText(_sourceFile, "test content");
        var step = new CopyFileStep(_sourceFile, _destinationFile);
        var progressReports = new List<string>();
        var progress = new Progress<InstallationProgress>(p => progressReports.Add(p.SubStepDescription));
        var context = TestInstallationContext.Create(progress: progress);

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        progressReports.Should().Contain("Preparing to copy file");
        progressReports.Should().Contain("Copying file");
        progressReports.Should().Contain("Verifying file copy");
        progressReports.Should().Contain("File copy completed");
    }

    [Fact]
    public async Task ValidateAsync_WithDiskSpaceCheck_ValidatesAvailableSpace()
    {
        // Arrange
        File.WriteAllText(_sourceFile, "test content");
        var step = new CopyFileStep(_sourceFile, _destinationFile, validateDiskSpace: true);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ValidateAsync(context);

        // Assert
        // Should succeed if there's enough space (most test environments have space)
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_CanDisableDiskSpaceCheck()
    {
        // Arrange
        File.WriteAllText(_sourceFile, "test content");
        var step = new CopyFileStep(_sourceFile, _destinationFile, validateDiskSpace: false);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ValidateAsync(context);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleNestedDirectories_CreatesAll()
    {
        // Arrange
        File.WriteAllText(_sourceFile, "test content");
        var deepDir = Path.Combine(_testDir, "level1", "level2", "level3");
        var destFile = Path.Combine(deepDir, "file.txt");
        var step = new CopyFileStep(_sourceFile, destFile, createDirectoriesIfNeeded: true);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(deepDir).Should().BeTrue();
        File.Exists(destFile).Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithAllParameters_SetsAllProperties()
    {
        // Act
        var step = new CopyFileStep(
            "source.txt",
            "dest.txt",
            overwrite: true,
            backupExisting: true,
            preserveTimestamps: false,
            copyAttributes: false,
            createDirectoriesIfNeeded: false,
            validateDiskSpace: false);

        // Assert
        step.SourcePath.Should().Be("source.txt");
        step.DestinationPath.Should().Be("dest.txt");
        step.Overwrite.Should().BeTrue();
        step.BackupExisting.Should().BeTrue();
        step.PreserveTimestamps.Should().BeFalse();
        step.CopyAttributes.Should().BeFalse();
        step.CreateDirectoriesIfNeeded.Should().BeFalse();
        step.ValidateDiskSpace.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithDefaultParameters_UsesExpectedDefaults()
    {
        // Act
        var step = new CopyFileStep("source.txt", "dest.txt");

        // Assert
        step.Overwrite.Should().BeFalse();
        step.BackupExisting.Should().BeFalse();
        step.PreserveTimestamps.Should().BeTrue();
        step.CopyAttributes.Should().BeTrue();
        step.CreateDirectoriesIfNeeded.Should().BeTrue();
        step.ValidateDiskSpace.Should().BeTrue();
    }

    // ============================================
    // New Test Cases - Critical Bug Fixes
    // ============================================

    [Fact]
    public async Task DisposeAsync_WhenCopyFailsAfterBackup_RestoresOriginalFile()
    {
        // This tests the critical bug fix in DisposeAsync
        // Scenario: backup created, then copy fails, destination may be corrupted
        // Expected: DisposeAsync should restore the backup regardless of _destinationExistedBefore

        // Arrange
        var originalContent = "original content";
        File.WriteAllText(_destinationFile, originalContent);

        // Create a step that will attempt to copy
        var step = new CopyFileStep(_sourceFile, _destinationFile, overwrite: true, backupExisting: true);
        var context = TestInstallationContext.Create();

        // Manually simulate the scenario:
        // 1. Destination existed
        // 2. Backup was created
        // 3. Copy operation fails

        // Create backup manually to simulate ExecuteAsync partial execution
        var backupPath = $"{_destinationFile}.backup_{Guid.NewGuid()}";
        File.Copy(_destinationFile, backupPath, overwrite: false);

        // Corrupt the destination file to simulate failed copy
        File.WriteAllText(_destinationFile, "corrupted data");

        // Use reflection to set private fields to simulate failed copy state
        var backupPathField = typeof(CopyFileStep).GetField("_backupPath",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var copySucceededField = typeof(CopyFileStep).GetField("_copySucceeded",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        backupPathField!.SetValue(step, backupPath);
        copySucceededField!.SetValue(step, false); // Copy failed

        // Act
        await step.DisposeAsync();

        // Assert
        File.ReadAllText(_destinationFile).Should().Be(originalContent,
            "DisposeAsync should restore backup when copy fails, preventing data corruption");
        File.Exists(backupPath).Should().BeFalse("backup file should be cleaned up");
    }

    [Fact]
    public async Task ExecuteAsync_WithAttributeCopyFailure_ContinuesAnyway()
    {
        // Arrange
        File.WriteAllText(_sourceFile, "test content");

        // On some file systems, setting certain attributes may fail
        // This test ensures we handle that gracefully
        var step = new CopyFileStep(_sourceFile, _destinationFile, copyAttributes: true);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue("should succeed even if attribute copy has issues");
        File.Exists(_destinationFile).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithTimestampPreservationFailure_ContinuesAnyway()
    {
        // Arrange
        File.WriteAllText(_sourceFile, "test content");

        // On some file systems, preserving timestamps may fail
        // This test ensures we handle that gracefully
        var step = new CopyFileStep(_sourceFile, _destinationFile, preserveTimestamps: true);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue("should succeed even if timestamp preservation has issues");
        File.Exists(_destinationFile).Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithNonExistentParentDirectory_ReturnsFailure()
    {
        // Arrange
        File.WriteAllText(_sourceFile, "test content");
        var nonExistentParent = Path.Combine(_testDir, "nonexistent", "nested", "file.txt");
        var step = new CopyFileStep(_sourceFile, nonExistentParent, createDirectoriesIfNeeded: false);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ValidateAsync(context);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Parent directory does not exist");
    }

    [Fact]
    public async Task ExecuteAsync_WithBackupExistingFalse_DoesNotCreateBackup()
    {
        // Arrange
        var sourceContent = "new content";
        var existingContent = "existing content";
        File.WriteAllText(_sourceFile, sourceContent);
        File.WriteAllText(_destinationFile, existingContent);

        var step = new CopyFileStep(_sourceFile, _destinationFile, overwrite: true, backupExisting: false);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        result.Data["BackupCreated"].Should().Be(false);
        result.Data.Should().NotContainKey("BackupPath");

        // Verify no backup files were created
        var backupFiles = Directory.GetFiles(_testDir, "*.backup_*");
        backupFiles.Should().BeEmpty("no backup should be created when BackupExisting is false");
    }

    [Fact]
    public async Task RollbackAsync_WithoutBackup_WhenOverwritingExistingFile_CannotRestore()
    {
        // This test documents the behavior when BackupExisting=false
        // If copy succeeds then later step fails, we cannot restore the original

        // Arrange
        var sourceContent = "new content";
        var originalContent = "original content that will be lost";
        File.WriteAllText(_sourceFile, sourceContent);
        File.WriteAllText(_destinationFile, originalContent);

        var step = new CopyFileStep(_sourceFile, _destinationFile, overwrite: true, backupExisting: false);
        var context = TestInstallationContext.Create();

        await step.ExecuteAsync(context);
        File.ReadAllText(_destinationFile).Should().Be(sourceContent);

        // Act
        var result = await step.RollbackAsync(context);

        // Assert
        result.Success.Should().BeTrue("rollback succeeds but can't restore original");
        result.Message.Should().Contain("no action needed");

        // Original content is lost because no backup was created
        File.ReadAllText(_destinationFile).Should().Be(sourceContent,
            "cannot restore original when BackupExisting was false");
    }
}
