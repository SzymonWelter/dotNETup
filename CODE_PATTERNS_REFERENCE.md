# CreateDirectoryStep - Code Patterns & Reference

This document provides code patterns and snippets to follow during implementation.

## Core Interface Implementation Pattern

```csharp
using DotNetUp.Core.Interfaces;
using DotNetUp.Core.Models;
using Microsoft.Extensions.Logging;

namespace DotNetUp.Steps.FileSystem;

/// <summary>
/// Installation step that creates directories with parent creation, permissions, and rollback support.
/// </summary>
public class CreateDirectoryStep : IInstallationStep
{
    /// <inheritdoc />
    public string Name => "CreateDirectory";

    /// <inheritdoc />
    public string Description => $"Create directory '{DirectoryPath}'";

    /// <summary>
    /// Gets the directory path to create.
    /// </summary>
    public string DirectoryPath { get; }

    // State tracking for rollback
    private bool _directoryExistedBefore;
    private int _parentDirectoriesCreated;
    private string? _originalPermissions;
    private bool _creationSucceeded;

    public CreateDirectoryStep(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("Directory path cannot be null or empty", nameof(directoryPath));

        DirectoryPath = directoryPath;
    }

    /// <inheritdoc />
    public Task<InstallationStepResult> ValidateAsync(InstallationContext context)
    {
        context.Logger.LogDebug("Validating CreateDirectoryStep: {Path}", DirectoryPath);

        try
        {
            // Path validation logic here

            context.Logger.LogDebug("Validation successful");
            return Task.FromResult(InstallationStepResult.SuccessResult("Validation successful"));
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "Validation failed");
            return Task.FromResult(InstallationStepResult.FailureResult("Error during validation", ex));
        }
    }

    /// <inheritdoc />
    public Task<InstallationStepResult> ExecuteAsync(InstallationContext context)
    {
        context.Logger.LogInformation("Executing CreateDirectoryStep: {Path}", DirectoryPath);

        try
        {
            // Execution logic here

            _creationSucceeded = true;
            context.Logger.LogInformation("Directory creation successful");
            return Task.FromResult(InstallationStepResult.SuccessResult("Success"));
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "Execution failed");
            return Task.FromResult(InstallationStepResult.FailureResult("Error during execution", ex));
        }
    }

    /// <inheritdoc />
    public Task<InstallationStepResult> RollbackAsync(InstallationContext context)
    {
        context.Logger.LogInformation("Rolling back CreateDirectoryStep: {Path}", DirectoryPath);

        try
        {
            // Rollback logic here

            context.Logger.LogInformation("Rollback successful");
            return Task.FromResult(InstallationStepResult.SuccessResult("Rollback successful"));
        }
        catch (Exception ex)
        {
            context.Logger.LogWarning(ex, "Rollback encountered an error (best-effort continues)");
            return Task.FromResult(InstallationStepResult.FailureResult("Rollback error", ex));
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        try
        {
            // Cleanup logic here (temp files, etc.)
        }
        catch
        {
            // Best-effort: silently ignore errors
        }

        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
```

## Configuration Extraction Pattern

```csharp
// Extract configuration from InstallationContext.Properties
private static (string directoryPath, bool createParents, bool allowExists) ExtractConfiguration(
    InstallationContext context)
{
    var directoryPath = context.Properties.TryGetValue("DirectoryPath", out var dpObj)
        ? dpObj as string
        : null;

    if (string.IsNullOrWhiteSpace(directoryPath))
        throw new InvalidOperationException("DirectoryPath not configured in context properties");

    var createParents = context.Properties.TryGetValue("CreateParentDirectories", out var cpObj)
        && cpObj is bool cpBool
        ? cpBool
        : true;  // Default: true

    var allowExists = context.Properties.TryGetValue("AllowIfAlreadyExists", out var aeObj)
        && aeObj is bool aeBool
        ? aeBool
        : false;  // Default: false

    return (directoryPath, createParents, allowExists);
}
```

## Path Validation Pattern

```csharp
private static void ValidatePath(string path, InstallationContext context)
{
    // Normalize path
    string normalizedPath;
    try
    {
        normalizedPath = Path.GetFullPath(path);
    }
    catch (ArgumentException ex)
    {
        throw new InvalidOperationException($"Invalid directory path: {path}", ex);
    }

    // Check path length
    const int MaxPathLength = 260;  // Windows standard
    if (normalizedPath.Length > MaxPathLength)
    {
        throw new InvalidOperationException(
            $"Directory path exceeds OS limit: {normalizedPath.Length} characters (max: {MaxPathLength})");
    }

    // Check for invalid characters
    var invalidChars = Path.GetInvalidPathChars();
    if (normalizedPath.Any(c => invalidChars.Contains(c)))
    {
        throw new InvalidOperationException(
            $"Directory path contains invalid characters: {path}");
    }

    // Additional validations...
}
```

## Parent Directory Creation Pattern

```csharp
private static int CreateParentDirectoriesIfNeeded(string directoryPath, InstallationContext context)
{
    var parentPath = Path.GetDirectoryName(directoryPath);
    if (string.IsNullOrEmpty(parentPath) || Directory.Exists(parentPath))
        return 0;

    int count = 0;
    try
    {
        Directory.CreateDirectory(parentPath);
        context.Logger.LogDebug("Created parent directory: {Parent}", parentPath);

        // If multiple levels needed, count them
        var dir = new DirectoryInfo(parentPath);
        count = 1;

        return count;
    }
    catch (IOException ex)
    {
        context.Logger.LogError(ex, "Failed to create parent directory: {Parent}", parentPath);
        throw;
    }
}
```

## Permission Backup & Restoration Pattern

```csharp
// Backup permissions before modification
private static string BackupPermissions(string directoryPath, InstallationContext context)
{
    try
    {
        var dirInfo = new DirectoryInfo(directoryPath);
        var permissions = dirInfo.GetAccessControl();

        // For now, store a simple string representation
        // In production, might serialize the ACL more formally
        var backupString = permissions.GetSecurityDescriptorSddlForm(
            System.Security.AccessControl.AccessControlSections.All);

        context.Logger.LogDebug("Backed up permissions for: {Path}", directoryPath);
        return backupString;
    }
    catch (Exception ex)
    {
        context.Logger.LogWarning(ex, "Could not backup permissions for: {Path}", directoryPath);
        return string.Empty;
    }
}

// Restore permissions from backup
private static void RestorePermissions(string directoryPath, string backup, InstallationContext context)
{
    if (string.IsNullOrEmpty(backup))
        return;

    try
    {
        var dirInfo = new DirectoryInfo(directoryPath);
        var acl = new System.Security.AccessControl.DirectorySecurity();
        acl.SetSecurityDescriptorSddlForm(backup);
        dirInfo.SetAccessControl(acl);

        context.Logger.LogDebug("Restored permissions for: {Path}", directoryPath);
    }
    catch (Exception ex)
    {
        context.Logger.LogWarning(ex, "Could not restore permissions for: {Path}", directoryPath);
        // Continue best-effort
    }
}
```

## Platform-Specific Permission Handling Pattern

```csharp
private static void SetPermissions(string directoryPath, string permissions, InstallationContext context)
{
    if (string.IsNullOrWhiteSpace(permissions))
        return;

    try
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            SetWindowsPermissions(directoryPath, permissions, context);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                 RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            SetUnixPermissions(directoryPath, permissions, context);
        }
    }
    catch (Exception ex)
    {
        // Permission setting is best-effort, log as warning not error
        context.Logger.LogWarning(ex, "Could not set permissions for: {Path}", directoryPath);
    }
}

private static void SetWindowsPermissions(string path, string permissions, InstallationContext context)
{
    try
    {
        // Parse Windows ACL format like "EVERYONE:Modify" or "SYSTEM:Full"
        var dirInfo = new DirectoryInfo(path);
        var acl = dirInfo.GetAccessControl();

        // Add rules based on permissions string
        // Example: "EVERYONE:Modify" would add Modify access for Everyone

        dirInfo.SetAccessControl(acl);
        context.Logger.LogDebug("Set Windows permissions for: {Path}", path);
    }
    catch (Exception ex)
    {
        context.Logger.LogWarning(ex, "Could not set Windows permissions");
        throw;
    }
}

private static void SetUnixPermissions(string path, string permissions, InstallationContext context)
{
    try
    {
        // Parse Unix format like "755" or "rwxr-xr-x"
        // Use Process to call chmod on Linux/macOS

        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"{permissions} \"{path}\"",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        process.WaitForExit();

        if (process.ExitCode == 0)
            context.Logger.LogDebug("Set Unix permissions for: {Path}", path);
        else
            context.Logger.LogWarning("chmod failed with exit code: {Code}", process.ExitCode);
    }
    catch (Exception ex)
    {
        context.Logger.LogWarning(ex, "Could not set Unix permissions");
        throw;
    }
}
```

## Rollback Directory Cleanup Pattern

```csharp
private static void CleanupCreatedDirectories(string directoryPath, int parentCountCreated,
    bool dirExistedBefore, InstallationContext context)
{
    // Don't remove if directory pre-existed
    if (dirExistedBefore)
        return;

    // Try to remove the target directory
    try
    {
        if (Directory.Exists(directoryPath))
        {
            if (IsDirectoryEmpty(directoryPath))
            {
                Directory.Delete(directoryPath);
                context.Logger.LogDebug("Removed created directory: {Path}", directoryPath);
            }
            else
            {
                context.Logger.LogWarning(
                    "Created directory contains files, leaving in place: {Path}", directoryPath);
                return;  // Don't try parent cleanup
            }
        }
    }
    catch (IOException ex)
    {
        context.Logger.LogWarning(ex, "Could not remove created directory: {Path}", directoryPath);
        return;  // Don't try parent cleanup if we can't remove target
    }

    // Walk back parent directory chain
    var currentPath = directoryPath;
    for (int i = 0; i < parentCountCreated; i++)
    {
        var parentPath = Path.GetDirectoryName(currentPath);
        if (string.IsNullOrEmpty(parentPath))
            break;

        try
        {
            if (Directory.Exists(parentPath) && IsDirectoryEmpty(parentPath))
            {
                Directory.Delete(parentPath);
                context.Logger.LogDebug("Removed empty parent directory: {Path}", parentPath);
                currentPath = parentPath;
            }
            else
            {
                // Stop at first non-empty parent
                break;
            }
        }
        catch (IOException ex)
        {
            context.Logger.LogWarning(ex, "Could not remove parent directory: {Path}", parentPath);
            break;  // Stop walking up on error
        }
    }
}

private static bool IsDirectoryEmpty(string path)
{
    try
    {
        return !Directory.EnumerateFileSystemEntries(path).Any();
    }
    catch
    {
        return false;  // Consider non-empty on error
    }
}
```

## Write Permission Test Pattern

```csharp
private static bool CanWriteToDirectory(string directoryPath, InstallationContext context)
{
    if (!Directory.Exists(directoryPath))
        return false;

    var testFile = Path.Combine(directoryPath, $".dotnetup_test_{Guid.NewGuid()}");

    try
    {
        File.WriteAllText(testFile, "test");
        File.Delete(testFile);
        return true;
    }
    catch (UnauthorizedAccessException)
    {
        return false;
    }
    catch (IOException)
    {
        return false;
    }
    finally
    {
        // Ensure test file is cleaned up
        try
        {
            if (File.Exists(testFile))
                File.Delete(testFile);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}
```

## Test Base Class Pattern

```csharp
using DotNetUp.Steps.FileSystem;
using DotNetUp.Tests.Fixtures;
using FluentAssertions;

namespace DotNetUp.Tests.Steps.FileSystem;

public class CreateDirectoryStepTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _testPath;

    public CreateDirectoryStepTests()
    {
        // Create unique test directory
        _testDir = Path.Combine(Path.GetTempPath(), $"DotNetUpTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);

        _testPath = Path.Combine(_testDir, "test_dir");
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

    // Test methods here...
}
```

## Test Method Pattern - Constructor

```csharp
[Fact]
public void Constructor_WithValidPath_SetsProperty()
{
    // Arrange & Act
    var step = new CreateDirectoryStep("/app/config");

    // Assert
    step.DirectoryPath.Should().Be("/app/config");
    step.Name.Should().Be("CreateDirectory");
    step.Description.Should().Contain("/app/config");
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
```

## Test Method Pattern - Validation

```csharp
[Fact]
public async Task ValidateAsync_WithValidPath_Succeeds()
{
    // Arrange
    var step = new CreateDirectoryStep(_testPath);
    var context = TestInstallationContext.Create();
    context.Properties["CreateParentDirectories"] = true;

    // Act
    var result = await step.ValidateAsync(context);

    // Assert
    result.Success.Should().BeTrue();
}

[Fact]
public async Task ValidateAsync_WithExistingDirectory_FailsWhenNotAllowed()
{
    // Arrange
    Directory.CreateDirectory(_testPath);
    var step = new CreateDirectoryStep(_testPath);
    var context = TestInstallationContext.Create();
    context.Properties["AllowIfAlreadyExists"] = false;

    // Act
    var result = await step.ValidateAsync(context);

    // Assert
    result.Success.Should().BeFalse();
}
```

## Test Method Pattern - Execution & Rollback

```csharp
[Fact]
public async Task ExecuteAsync_CreatesDirectoryAndRollback_RemovesIt()
{
    // Arrange
    var step = new CreateDirectoryStep(_testPath);
    var context = TestInstallationContext.Create();
    context.Properties["DirectoryPath"] = _testPath;
    context.Properties["CreateParentDirectories"] = true;

    // Act - Execute
    await step.ValidateAsync(context);
    var executeResult = await step.ExecuteAsync(context);

    // Assert - Directory created
    executeResult.Success.Should().BeTrue();
    Directory.Exists(_testPath).Should().BeTrue();

    // Act - Rollback
    var rollbackResult = await step.RollbackAsync(context);

    // Assert - Directory removed
    rollbackResult.Success.Should().BeTrue();
    Directory.Exists(_testPath).Should().BeFalse();
}
```

## Error Handling Pattern

```csharp
try
{
    // Operation
}
catch (ArgumentException ex)
{
    // Expected validation errors
    context.Logger.LogWarning(ex, "Validation error");
    return InstallationStepResult.FailureResult($"Validation failed: {ex.Message}", ex);
}
catch (IOException ex)
{
    // File system errors
    context.Logger.LogError(ex, "File system error");
    return InstallationStepResult.FailureResult($"File system error: {ex.Message}", ex);
}
catch (UnauthorizedAccessException ex)
{
    // Permission errors
    context.Logger.LogError(ex, "Permission error");
    return InstallationStepResult.FailureResult($"Permission denied: {ex.Message}", ex);
}
catch (Exception ex)
{
    // Unexpected errors
    context.Logger.LogError(ex, "Unexpected error");
    return InstallationStepResult.FailureResult($"Unexpected error: {ex.Message}", ex);
}
```

---

## Key Patterns Summary

1. **Always track state** - Store _directoryExistedBefore, _parentDirectoriesCreated for rollback
2. **Use context.Properties** - Extract configuration and store results
3. **Log at key points** - Debug for entry/decisions, Info for success, Error for failures
4. **Best-effort rollback** - Return success even if partial failures
5. **Graceful degradation** - Permission setting failures logged as warnings, not errors
6. **Platform awareness** - Check Windows vs Unix, handle accordingly
7. **Exception specificity** - Catch specific exceptions, not generic Exception
8. **Temp file cleanup** - Always clean up test files in finally blocks
9. **Empty directory checks** - Use EnumerateFileSystemEntries for efficiency
10. **Context.Properties population** - Store results for other steps to read

---

**Reference Document Created**: 2026-01-12
**For Implementation**: Use these patterns when writing CreateDirectoryStep.cs and CreateDirectoryStepTests.cs
