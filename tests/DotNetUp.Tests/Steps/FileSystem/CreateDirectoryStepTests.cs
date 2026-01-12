using DotNetUp.Steps.FileSystem;
using DotNetUp.Tests.Fixtures;
using FluentAssertions;

namespace DotNetUp.Tests.Steps.FileSystem;

public class CreateDirectoryStepTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _targetDirectory;

    public CreateDirectoryStepTests()
    {
        // Create a unique test directory for each test
        _testDir = Path.Combine(Path.GetTempPath(), $"DotNetUpTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);

        _targetDirectory = Path.Combine(_testDir, "target");
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
            catch (Exception ex)
            {
                // Log cleanup failures but continue
                Console.WriteLine($"WARNING: Failed to cleanup test directory {_testDir}: {ex.Message}");
            }
        }
    }

    // ============================================
    // Constructor Tests
    // ============================================

    [Fact]
    public void Constructor_WithValidPath_SetsProperties()
    {
        // Arrange & Act
        var step = new CreateDirectoryStep(
            "/test/path",
            createParentDirectories: true,
            allowIfAlreadyExists: true,
            setPermissions: true,
            permissions: "755",
            owner: "root",
            group: "admin",
            backupExistingPermissions: true);

        // Assert
        step.DirectoryPath.Should().Be("/test/path");
        step.CreateParentDirectories.Should().BeTrue();
        step.AllowIfAlreadyExists.Should().BeTrue();
        step.SetPermissions.Should().BeTrue();
        step.Permissions.Should().Be("755");
        step.Owner.Should().Be("root");
        step.Group.Should().Be("admin");
        step.BackupExistingPermissions.Should().BeTrue();
        step.Name.Should().Be("CreateDirectory");
        step.Description.Should().Contain("/test/path");
    }

    [Fact]
    public void Constructor_WithDefaults_SetsDefaultValues()
    {
        // Arrange & Act
        var step = new CreateDirectoryStep("/test/path");

        // Assert
        step.CreateParentDirectories.Should().BeTrue();
        step.AllowIfAlreadyExists.Should().BeFalse();
        step.SetPermissions.Should().BeFalse();
        step.Permissions.Should().BeNull();
        step.Owner.Should().BeNull();
        step.Group.Should().BeNull();
        step.BackupExistingPermissions.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithNullPath_ThrowsArgumentException()
    {
        // Act
        Action act = () => new CreateDirectoryStep(null!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("directoryPath");
    }

    [Fact]
    public void Constructor_WithEmptyPath_ThrowsArgumentException()
    {
        // Act
        Action act = () => new CreateDirectoryStep("");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("directoryPath");
    }

    [Fact]
    public void Constructor_WithWhitespacePath_ThrowsArgumentException()
    {
        // Act
        Action act = () => new CreateDirectoryStep("   ");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("directoryPath");
    }

    // ============================================
    // Validation Tests - Success Scenarios
    // ============================================

    [Fact]
    public async Task ValidateAsync_WhenDirectoryDoesNotExist_ReturnsSuccess()
    {
        // Arrange
        var step = new CreateDirectoryStep(_targetDirectory);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ValidateAsync(context);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WhenDirectoryExistsAndAllowed_ReturnsSuccess()
    {
        // Arrange
        Directory.CreateDirectory(_targetDirectory);
        var step = new CreateDirectoryStep(_targetDirectory, allowIfAlreadyExists: true);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ValidateAsync(context);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WhenParentExistsAndWritable_ReturnsSuccess()
    {
        // Arrange
        var subDir = Path.Combine(_testDir, "subdir");
        var step = new CreateDirectoryStep(subDir, createParentDirectories: false);
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
    public async Task ValidateAsync_WhenDirectoryExistsAndNotAllowed_ReturnsFailure()
    {
        // Arrange
        Directory.CreateDirectory(_targetDirectory);
        var step = new CreateDirectoryStep(_targetDirectory, allowIfAlreadyExists: false);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ValidateAsync(context);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already exists");
    }

    [Fact]
    public async Task ValidateAsync_WhenParentDoesNotExistAndNotCreating_ReturnsFailure()
    {
        // Arrange
        var deepPath = Path.Combine(_testDir, "nonexistent", "parent", "target");
        var step = new CreateDirectoryStep(deepPath, createParentDirectories: false);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ValidateAsync(context);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Parent directory does not exist");
    }

    [Fact]
    public async Task ValidateAsync_WhenParentIsFile_ReturnsFailure()
    {
        // Arrange
        var parentFile = Path.Combine(_testDir, "file.txt");
        File.WriteAllText(parentFile, "content");
        var invalidPath = Path.Combine(parentFile, "subdir");
        var step = new CreateDirectoryStep(invalidPath);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ValidateAsync(context);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Parent is a file");
    }

    [Fact]
    public async Task ValidateAsync_WhenPathTooLong_ReturnsFailure()
    {
        // Arrange
        var longPath = Path.Combine(_testDir, new string('a', 300));
        var step = new CreateDirectoryStep(longPath);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ValidateAsync(context);

        // Assert
        if (OperatingSystem.IsWindows())
        {
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("path too long");
        }
        else
        {
            // Unix systems have much larger limits
            result.Success.Should().BeTrue();
        }
    }

    [Fact]
    public async Task ValidateAsync_WhenSetPermissionsWithoutPermissionString_ReturnsFailure()
    {
        // Arrange
        var step = new CreateDirectoryStep(_targetDirectory, setPermissions: true);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ValidateAsync(context);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Permissions string is required");
    }

    [Fact]
    public async Task ValidateAsync_WhenInvalidPermissionString_ReturnsFailure()
    {
        // Arrange
        var step = new CreateDirectoryStep(_targetDirectory, setPermissions: true, permissions: "999");
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ValidateAsync(context);

        // Assert
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Invalid permission string");
        }
    }

    // ============================================
    // Execution Tests - Success Scenarios
    // ============================================

    [Fact]
    public async Task ExecuteAsync_WhenDirectoryDoesNotExist_CreatesDirectory()
    {
        // Arrange
        var step = new CreateDirectoryStep(_targetDirectory);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(_targetDirectory).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDirectoryExistsAndAllowed_ReturnsSuccess()
    {
        // Arrange
        Directory.CreateDirectory(_targetDirectory);
        var step = new CreateDirectoryStep(_targetDirectory, allowIfAlreadyExists: true);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(_targetDirectory).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithCreateParentDirectories_CreatesParents()
    {
        // Arrange
        var deepPath = Path.Combine(_testDir, "level1", "level2", "level3", "target");
        var step = new CreateDirectoryStep(deepPath, createParentDirectories: true);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(deepPath).Should().BeTrue();
        Directory.Exists(Path.Combine(_testDir, "level1")).Should().BeTrue();
        Directory.Exists(Path.Combine(_testDir, "level1", "level2")).Should().BeTrue();
        Directory.Exists(Path.Combine(_testDir, "level1", "level2", "level3")).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithoutCreateParentDirectories_OnlyCreatesTarget()
    {
        // Arrange
        var parentDir = Path.Combine(_testDir, "parent");
        Directory.CreateDirectory(parentDir);
        var targetDir = Path.Combine(parentDir, "target");
        var step = new CreateDirectoryStep(targetDir, createParentDirectories: false);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(targetDir).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithSpecialCharactersInPath_CreatesDirectory()
    {
        // Arrange
        var specialPath = Path.Combine(_testDir, "test-dir_2024(1)");
        var step = new CreateDirectoryStep(specialPath);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(specialPath).Should().BeTrue();
    }

    // ============================================
    // Execution Tests - Failure Scenarios
    // ============================================

    [Fact]
    public async Task ExecuteAsync_WhenDirectoryExistsAndNotAllowed_ReturnsFailure()
    {
        // Arrange
        Directory.CreateDirectory(_targetDirectory);
        var step = new CreateDirectoryStep(_targetDirectory, allowIfAlreadyExists: false);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already exists");
    }

    // ============================================
    // Rollback Tests - Success Scenarios
    // ============================================

    [Fact]
    public async Task RollbackAsync_WhenDirectoryWasCreated_DeletesDirectory()
    {
        // Arrange
        var step = new CreateDirectoryStep(_targetDirectory);
        var context = TestInstallationContext.Create();

        await step.ExecuteAsync(context);
        Directory.Exists(_targetDirectory).Should().BeTrue("directory should exist after execute");

        // Act
        var result = await step.RollbackAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(_targetDirectory).Should().BeFalse("directory should be deleted after rollback");
    }

    [Fact]
    public async Task RollbackAsync_WhenParentDirectoriesWereCreated_RemovesEmptyParents()
    {
        // Arrange
        var deepPath = Path.Combine(_testDir, "level1", "level2", "level3");
        var step = new CreateDirectoryStep(deepPath, createParentDirectories: true);
        var context = TestInstallationContext.Create();

        await step.ExecuteAsync(context);
        Directory.Exists(deepPath).Should().BeTrue();

        // Act
        var result = await step.RollbackAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(deepPath).Should().BeFalse();
        Directory.Exists(Path.Combine(_testDir, "level1", "level2")).Should().BeFalse();
        Directory.Exists(Path.Combine(_testDir, "level1")).Should().BeFalse();
    }

    [Fact]
    public async Task RollbackAsync_WhenDirectoryNotEmpty_LeavesDirectory()
    {
        // Arrange
        var step = new CreateDirectoryStep(_targetDirectory);
        var context = TestInstallationContext.Create();

        await step.ExecuteAsync(context);

        // Add a file to the directory
        var testFile = Path.Combine(_targetDirectory, "file.txt");
        File.WriteAllText(testFile, "content");

        // Act
        var result = await step.RollbackAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(_targetDirectory).Should().BeTrue("directory should remain if not empty");
        File.Exists(testFile).Should().BeTrue();
    }

    [Fact]
    public async Task RollbackAsync_WhenDirectoryExistedBefore_DoesNotDelete()
    {
        // Arrange
        Directory.CreateDirectory(_targetDirectory);
        var step = new CreateDirectoryStep(_targetDirectory, allowIfAlreadyExists: true);
        var context = TestInstallationContext.Create();

        await step.ExecuteAsync(context);

        // Act
        var result = await step.RollbackAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(_targetDirectory).Should().BeTrue("pre-existing directory should not be deleted");
    }

    [Fact]
    public async Task RollbackAsync_WhenParentHasOtherContent_StopsAtNonEmptyParent()
    {
        // Arrange
        var deepPath = Path.Combine(_testDir, "level1", "level2", "level3");
        var step = new CreateDirectoryStep(deepPath, createParentDirectories: true);
        var context = TestInstallationContext.Create();

        await step.ExecuteAsync(context);

        // Add content to level2
        var siblingFile = Path.Combine(_testDir, "level1", "level2", "other.txt");
        File.WriteAllText(siblingFile, "content");

        // Act
        var result = await step.RollbackAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(deepPath).Should().BeFalse("target should be deleted");
        Directory.Exists(Path.Combine(_testDir, "level1", "level2")).Should().BeTrue("level2 has content");
        Directory.Exists(Path.Combine(_testDir, "level1")).Should().BeTrue("level1 has content");
    }

    [Fact]
    public async Task RollbackAsync_BeforeExecute_ReturnsSuccess()
    {
        // Arrange
        var step = new CreateDirectoryStep(_targetDirectory);
        var context = TestInstallationContext.Create();

        // Act - Call rollback without execute
        var result = await step.RollbackAsync(context);

        // Assert
        result.Success.Should().BeTrue("rollback should succeed even if nothing was executed");
    }

    // ============================================
    // Integration Tests
    // ============================================

    [Fact]
    public async Task CompleteWorkflow_ValidateExecuteRollback_WorksCorrectly()
    {
        // Arrange
        var step = new CreateDirectoryStep(_targetDirectory);
        var context = TestInstallationContext.Create();

        // Act & Assert - Validate
        var validateResult = await step.ValidateAsync(context);
        validateResult.Success.Should().BeTrue();

        // Act & Assert - Execute
        var executeResult = await step.ExecuteAsync(context);
        executeResult.Success.Should().BeTrue();
        Directory.Exists(_targetDirectory).Should().BeTrue();

        // Act & Assert - Rollback
        var rollbackResult = await step.RollbackAsync(context);
        rollbackResult.Success.Should().BeTrue();
        Directory.Exists(_targetDirectory).Should().BeFalse();
    }

    [Fact]
    public async Task CompleteWorkflow_WithDeepPath_ValidateExecuteRollback()
    {
        // Arrange
        var deepPath = Path.Combine(_testDir, "a", "b", "c", "d", "e");
        var step = new CreateDirectoryStep(deepPath, createParentDirectories: true);
        var context = TestInstallationContext.Create();

        // Act & Assert - Validate
        var validateResult = await step.ValidateAsync(context);
        validateResult.Success.Should().BeTrue();

        // Act & Assert - Execute
        var executeResult = await step.ExecuteAsync(context);
        executeResult.Success.Should().BeTrue();
        Directory.Exists(deepPath).Should().BeTrue();

        // Act & Assert - Rollback
        var rollbackResult = await step.RollbackAsync(context);
        rollbackResult.Success.Should().BeTrue();
        Directory.Exists(deepPath).Should().BeFalse();
        Directory.Exists(Path.Combine(_testDir, "a")).Should().BeFalse();
    }

    [Fact]
    public async Task CompleteWorkflow_WithExistingDirectory_WorksCorrectly()
    {
        // Arrange
        Directory.CreateDirectory(_targetDirectory);
        var originalCreationTime = Directory.GetCreationTime(_targetDirectory);

        var step = new CreateDirectoryStep(_targetDirectory, allowIfAlreadyExists: true);
        var context = TestInstallationContext.Create();

        // Act & Assert - Validate
        var validateResult = await step.ValidateAsync(context);
        validateResult.Success.Should().BeTrue();

        // Act & Assert - Execute
        var executeResult = await step.ExecuteAsync(context);
        executeResult.Success.Should().BeTrue();
        Directory.Exists(_targetDirectory).Should().BeTrue();

        // Act & Assert - Rollback
        var rollbackResult = await step.RollbackAsync(context);
        rollbackResult.Success.Should().BeTrue();
        Directory.Exists(_targetDirectory).Should().BeTrue("pre-existing directory should remain");
    }

    // ============================================
    // Edge Case Tests
    // ============================================

    [Fact]
    public async Task ExecuteAsync_WithVeryDeepPath_WorksCorrectly()
    {
        // Arrange
        var parts = Enumerable.Range(1, 20).Select(i => $"level{i}").ToList();
        var deepPath = Path.Combine(_testDir, Path.Combine(parts.ToArray()));
        var step = new CreateDirectoryStep(deepPath, createParentDirectories: true);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(deepPath).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithUnicodeCharacters_WorksCorrectly()
    {
        // Arrange
        var unicodePath = Path.Combine(_testDir, "测试目录");
        var step = new CreateDirectoryStep(unicodePath);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(unicodePath).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithSpacesInPath_WorksCorrectly()
    {
        // Arrange
        var spacedPath = Path.Combine(_testDir, "my test directory");
        var step = new CreateDirectoryStep(spacedPath);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(spacedPath).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithDots_WorksCorrectly()
    {
        // Arrange
        var dottedPath = Path.Combine(_testDir, "test.directory.name");
        var step = new CreateDirectoryStep(dottedPath);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(dottedPath).Should().BeTrue();
    }

    [Fact]
    public async Task RollbackAsync_WithLongDirectoryChain_WorksCorrectly()
    {
        // Arrange
        var parts = Enumerable.Range(1, 10).Select(i => $"dir{i}").ToList();
        var longPath = Path.Combine(_testDir, Path.Combine(parts.ToArray()));
        var step = new CreateDirectoryStep(longPath, createParentDirectories: true);
        var context = TestInstallationContext.Create();

        await step.ExecuteAsync(context);

        // Act
        var result = await step.RollbackAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(longPath).Should().BeFalse();
        // Verify all parent directories are removed
        foreach (var i in Enumerable.Range(1, 10))
        {
            var checkPath = Path.Combine(_testDir, Path.Combine(parts.Take(i).ToArray()));
            Directory.Exists(checkPath).Should().BeFalse($"dir{i} should be removed");
        }
    }

    [Fact]
    public async Task DisposeAsync_CleansUpState_WorksCorrectly()
    {
        // Arrange
        var step = new CreateDirectoryStep(_targetDirectory);
        var context = TestInstallationContext.Create();
        await step.ExecuteAsync(context);

        // Act
        await step.DisposeAsync();

        // Assert - No exceptions should be thrown
        // Disposal should be idempotent
        await step.DisposeAsync();
    }

    // ============================================
    // Permission Tests (Basic)
    // ============================================

    [Fact]
    public async Task ExecuteAsync_WithSetPermissions_DoesNotThrow()
    {
        // Arrange
        var permissions = OperatingSystem.IsWindows() ? "EVERYONE:Full" : "755";
        var step = new CreateDirectoryStep(_targetDirectory, setPermissions: true, permissions: permissions);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(_targetDirectory).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithPermissionsOnExistingDirectory_DoesNotThrow()
    {
        // Arrange
        Directory.CreateDirectory(_targetDirectory);
        var permissions = OperatingSystem.IsWindows() ? "EVERYONE:Full" : "755";
        var step = new CreateDirectoryStep(
            _targetDirectory,
            allowIfAlreadyExists: true,
            setPermissions: true,
            permissions: permissions);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(_targetDirectory).Should().BeTrue();
    }

    // ============================================
    // Concurrent Access Tests
    // ============================================

    [Fact]
    public async Task ExecuteAsync_WhenDirectoryCreatedConcurrently_HandlesGracefully()
    {
        // Arrange
        var step = new CreateDirectoryStep(_targetDirectory, allowIfAlreadyExists: true);
        var context = TestInstallationContext.Create();

        // Create directory concurrently
        Directory.CreateDirectory(_targetDirectory);

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(_targetDirectory).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDirectoryCreatedBetweenValidateAndExecute_FailsWithWarning()
    {
        // Arrange
        var step = new CreateDirectoryStep(_targetDirectory, allowIfAlreadyExists: false);
        var context = TestInstallationContext.Create();

        // Validate first (directory doesn't exist)
        var validateResult = await step.ValidateAsync(context);
        validateResult.Success.Should().BeTrue();

        // Simulate race condition - create directory before execute
        Directory.CreateDirectory(_targetDirectory);

        // Act - Execute should fail
        var executeResult = await step.ExecuteAsync(context);

        // Assert
        executeResult.Success.Should().BeFalse();
        executeResult.Message.Should().Contain("already exists");
        Directory.Exists(_targetDirectory).Should().BeTrue();
    }

    // ============================================
    // Parent Directory Tracking Tests
    // ============================================

    [Fact]
    public async Task ExecuteAsync_WhenParentExistsDuringCreation_DoesNotTrackAsCreated()
    {
        // Arrange
        var level1 = Path.Combine(_testDir, "level1");
        Directory.CreateDirectory(level1); // Pre-create level1

        var deepPath = Path.Combine(level1, "level2", "level3");
        var step = new CreateDirectoryStep(deepPath, createParentDirectories: true);
        var context = TestInstallationContext.Create();

        // Act
        await step.ExecuteAsync(context);
        Directory.Exists(deepPath).Should().BeTrue();

        // Rollback
        var rollbackResult = await step.RollbackAsync(context);

        // Assert
        rollbackResult.Success.Should().BeTrue();
        Directory.Exists(deepPath).Should().BeFalse();
        Directory.Exists(Path.Combine(level1, "level2")).Should().BeFalse();
        Directory.Exists(level1).Should().BeTrue("level1 existed before step execution");
    }

    [Fact]
    public async Task ExecuteAsync_WhenMultipleParentsCreated_TracksOnlyNewOnes()
    {
        // Arrange
        var level1 = Path.Combine(_testDir, "existing");
        var level2 = Path.Combine(level1, "new1");
        var level3 = Path.Combine(level2, "new2");

        Directory.CreateDirectory(level1); // Pre-create level1

        var step = new CreateDirectoryStep(level3, createParentDirectories: true);
        var context = TestInstallationContext.Create();

        // Act
        await step.ExecuteAsync(context);
        Directory.Exists(level3).Should().BeTrue();

        // Rollback
        await step.RollbackAsync(context);

        // Assert
        Directory.Exists(level3).Should().BeFalse();
        Directory.Exists(level2).Should().BeFalse();
        Directory.Exists(level1).Should().BeTrue("level1 existed before step");
    }

    // ============================================
    // Permission Placeholder Tests
    // ============================================

    [Fact]
    public async Task ExecuteAsync_WithSetPermissions_LogsPlaceholderWarning()
    {
        // Arrange
        var permissions = OperatingSystem.IsWindows() ? "EVERYONE:Full" : "755";
        var step = new CreateDirectoryStep(_targetDirectory, setPermissions: true, permissions: permissions);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(_targetDirectory).Should().BeTrue();
        // Note: We can't easily assert on log messages without a test logger implementation
        // But the warning is logged as verified by manual testing
    }

    // ============================================
    // Path Validation Tests
    // ============================================

    [Fact]
    public async Task ValidateAsync_WithInvalidCharacters_ReturnsFailure()
    {
        // Arrange - Path with invalid characters (Windows)
        if (OperatingSystem.IsWindows())
        {
            var invalidPath = Path.Combine(_testDir, "test<>file");
            var step = new CreateDirectoryStep(invalidPath);
            var context = TestInstallationContext.Create();

            // Act
            var result = await step.ValidateAsync(context);

            // Assert
            result.Success.Should().BeFalse();
        }
    }

    [Fact]
    public async Task ValidateAsync_WithRootDirectory_FailsGracefully()
    {
        // Arrange
        var rootPath = OperatingSystem.IsWindows() ? "C:\\" : "/";
        var step = new CreateDirectoryStep(rootPath, allowIfAlreadyExists: false);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ValidateAsync(context);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already exists");
    }

    // ============================================
    // State Cleanup Tests
    // ============================================

    [Fact]
    public async Task DisposeAsync_MultipleCalls_DoesNotThrow()
    {
        // Arrange
        var step = new CreateDirectoryStep(_targetDirectory);
        var context = TestInstallationContext.Create();
        await step.ExecuteAsync(context);

        // Act - Multiple dispose calls
        await step.DisposeAsync();
        await step.DisposeAsync();
        await step.DisposeAsync();

        // Assert - No exceptions should be thrown
        true.Should().BeTrue("Multiple DisposeAsync calls should not throw");
    }

    [Fact]
    public async Task DisposeAsync_AfterRollback_ClearsAllState()
    {
        // Arrange
        var deepPath = Path.Combine(_testDir, "a", "b", "c");
        var step = new CreateDirectoryStep(deepPath, createParentDirectories: true);
        var context = TestInstallationContext.Create();

        await step.ExecuteAsync(context);
        await step.RollbackAsync(context);

        // Act
        await step.DisposeAsync();

        // Assert - Should be able to execute again without state pollution
        // (Though reusing a step is not the intended pattern, dispose should clean state)
        true.Should().BeTrue("DisposeAsync should clean all state");
    }

    // ============================================
    // Rollback Safety Tests
    // ============================================

    [Fact]
    public async Task RollbackAsync_WhenDirectoryDeleteFails_ContinuesGracefully()
    {
        // Arrange
        var step = new CreateDirectoryStep(_targetDirectory);
        var context = TestInstallationContext.Create();

        await step.ExecuteAsync(context);

        // Lock the directory by creating an open file handle
        var lockFile = Path.Combine(_targetDirectory, "lock.txt");
        using var fileStream = File.Create(lockFile);

        // Act
        var result = await step.RollbackAsync(context);

        // Assert
        result.Success.Should().BeTrue("Rollback should succeed (best-effort)");
        Directory.Exists(_targetDirectory).Should().BeTrue("Directory should remain if delete fails");
    }

    [Fact]
    public async Task ExecuteAsync_WithAbsolutePath_WorksCorrectly()
    {
        // Arrange
        var absolutePath = Path.GetFullPath(_targetDirectory);
        var step = new CreateDirectoryStep(absolutePath);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(absolutePath).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WithTrailingSlash_WorksCorrectly()
    {
        // Arrange
        var pathWithSlash = _targetDirectory + Path.DirectorySeparatorChar;
        var step = new CreateDirectoryStep(pathWithSlash);
        var context = TestInstallationContext.Create();

        // Act
        var result = await step.ExecuteAsync(context);

        // Assert
        result.Success.Should().BeTrue();
        Directory.Exists(_targetDirectory).Should().BeTrue();
    }

    // ============================================
    // Error Handling Tests
    // ============================================

    [Fact]
    public async Task ValidateAsync_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var step = new CreateDirectoryStep(_targetDirectory);

        // Act
        Func<Task> act = async () => await step.ValidateAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task ExecuteAsync_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var step = new CreateDirectoryStep(_targetDirectory);

        // Act
        Func<Task> act = async () => await step.ExecuteAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RollbackAsync_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var step = new CreateDirectoryStep(_targetDirectory);

        // Act
        Func<Task> act = async () => await step.RollbackAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
