using DotNetUp.Core.Interfaces;
using DotNetUp.Core.Models;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace DotNetUp.Steps.FileSystem;

/// <summary>
/// Installation step that creates a directory with support for parent directory creation,
/// permission management, and comprehensive rollback capabilities.
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

    /// <summary>
    /// Gets a value indicating whether to create parent directories automatically.
    /// </summary>
    public bool CreateParentDirectories { get; }

    /// <summary>
    /// Gets a value indicating whether to allow the operation if the directory already exists.
    /// </summary>
    public bool AllowIfAlreadyExists { get; }

    /// <summary>
    /// Gets a value indicating whether to set specific permissions after creation.
    /// WARNING: Permission operations are currently placeholder implementations and do not modify actual file system permissions.
    /// This feature is planned for future implementation.
    /// </summary>
    public bool SetPermissions { get; }

    /// <summary>
    /// Gets the permission string to set (e.g., "755" on Unix, "EVERYONE:Full" on Windows).
    /// WARNING: Permission operations are currently placeholder implementations and do not modify actual file system permissions.
    /// This feature is planned for future implementation.
    /// </summary>
    public string? Permissions { get; }

    /// <summary>
    /// Gets the owner/user to set (Unix-like systems).
    /// </summary>
    public string? Owner { get; }

    /// <summary>
    /// Gets the group ownership to set (Unix-like systems).
    /// </summary>
    public string? Group { get; }

    /// <summary>
    /// Gets a value indicating whether to backup existing permissions before changing.
    /// </summary>
    public bool BackupExistingPermissions { get; }

    // State tracking for rollback
    private bool _directoryExistedBefore;
    private bool _directoryCreated;
    private bool _permissionsChanged;
    private List<string> _createdParentDirectories = new();
    private string? _originalPermissions;

    /// <summary>
    /// Creates a new create directory step.
    /// </summary>
    /// <param name="directoryPath">Path to the directory to create</param>
    /// <param name="createParentDirectories">Whether to create parent directories automatically (default: true)</param>
    /// <param name="allowIfAlreadyExists">Whether to allow if directory already exists (default: false)</param>
    /// <param name="setPermissions">Whether to set specific permissions (default: false)</param>
    /// <param name="permissions">Permission string to set (default: null)</param>
    /// <param name="owner">Owner/user to set (default: null)</param>
    /// <param name="group">Group ownership to set (default: null)</param>
    /// <param name="backupExistingPermissions">Whether to backup existing permissions (default: false)</param>
    /// <exception cref="ArgumentException">Thrown when directory path is null or whitespace</exception>
    public CreateDirectoryStep(
        string directoryPath,
        bool createParentDirectories = true,
        bool allowIfAlreadyExists = false,
        bool setPermissions = false,
        string? permissions = null,
        string? owner = null,
        string? group = null,
        bool backupExistingPermissions = false)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("Directory path cannot be null or empty", nameof(directoryPath));

        DirectoryPath = directoryPath;
        CreateParentDirectories = createParentDirectories;
        AllowIfAlreadyExists = allowIfAlreadyExists;
        SetPermissions = setPermissions;
        Permissions = permissions;
        Owner = owner;
        Group = group;
        BackupExistingPermissions = backupExistingPermissions;
    }

    /// <inheritdoc />
    public Task<InstallationStepResult> ValidateAsync(InstallationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Logger.LogDebug("Validating CreateDirectoryStep: {DirectoryPath}", DirectoryPath);

        try
        {
            // Validate path is not too long
            if (DirectoryPath.Length > GetMaxPathLength())
            {
                return Task.FromResult(InstallationStepResult.FailureResult(
                    $"Directory path too long: {DirectoryPath} (max length: {GetMaxPathLength()})"));
            }

            // Validate path contains only valid characters
            if (ContainsInvalidPathCharacters(DirectoryPath))
            {
                return Task.FromResult(InstallationStepResult.FailureResult(
                    $"Invalid directory path: {DirectoryPath}"));
            }

            // Check if directory already exists
            var directoryExists = Directory.Exists(DirectoryPath);
            if (directoryExists && !AllowIfAlreadyExists)
            {
                return Task.FromResult(InstallationStepResult.FailureResult(
                    $"Directory already exists: {DirectoryPath}. Set AllowIfAlreadyExists to true to allow."));
            }

            // If directory doesn't exist, validate parent directory
            if (!directoryExists)
            {
                var parentPath = Path.GetDirectoryName(DirectoryPath);

                // Check if parent is actually a file
                if (!string.IsNullOrEmpty(parentPath) && File.Exists(parentPath))
                {
                    return Task.FromResult(InstallationStepResult.FailureResult(
                        $"Parent is a file, not a directory: {parentPath}"));
                }

                // Check if parent directory exists or can be created
                if (!string.IsNullOrEmpty(parentPath) && !Directory.Exists(parentPath))
                {
                    if (!CreateParentDirectories)
                    {
                        return Task.FromResult(InstallationStepResult.FailureResult(
                            $"Parent directory does not exist: {parentPath}. Set CreateParentDirectories to true to create it."));
                    }
                }
                // Parent directory exists, it will be validated during execution
                // We don't perform write tests during validation to avoid modifying the system
            }

            // Validate permissions if setting them
            if (SetPermissions)
            {
                if (string.IsNullOrWhiteSpace(Permissions))
                {
                    return Task.FromResult(InstallationStepResult.FailureResult(
                        "Permissions string is required when SetPermissions is true"));
                }

                if (!IsValidPermissionString(Permissions))
                {
                    return Task.FromResult(InstallationStepResult.FailureResult(
                        $"Invalid permission string: {Permissions}"));
                }
            }

            // Validate disk space (minimal check for directory metadata)
            try
            {
                var rootPath = Path.GetPathRoot(DirectoryPath);
                if (!string.IsNullOrEmpty(rootPath))
                {
                    var driveInfo = new DriveInfo(rootPath);
                    if (driveInfo.AvailableFreeSpace < 1024) // 1KB minimum
                    {
                        return Task.FromResult(InstallationStepResult.FailureResult(
                            "Insufficient disk space"));
                    }
                }
            }
            catch (Exception ex)
            {
                context.Logger.LogWarning(ex, "Could not check disk space");
                // Continue validation - this is not critical
            }

            context.Logger.LogDebug("Validation successful for CreateDirectoryStep");
            return Task.FromResult(InstallationStepResult.SuccessResult("Validation successful"));
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "Unexpected error during validation");
            return Task.FromResult(InstallationStepResult.FailureResult(
                "Unexpected error during validation",
                ex));
        }
    }

    /// <inheritdoc />
    public Task<InstallationStepResult> ExecuteAsync(InstallationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Logger.LogInformation("Executing CreateDirectoryStep: {DirectoryPath}", DirectoryPath);

        try
        {
            // Check if directory already exists
            _directoryExistedBefore = Directory.Exists(DirectoryPath);

            if (_directoryExistedBefore)
            {
                if (AllowIfAlreadyExists)
                {
                    context.Logger.LogInformation("Directory already exists: {DirectoryPath}", DirectoryPath);

                    // Still set permissions if requested
                    if (SetPermissions)
                    {
                        SetDirectoryPermissions(context);
                    }

                    return Task.FromResult(InstallationStepResult.SuccessResult(
                        $"Directory already exists: {DirectoryPath}"));
                }
                else
                {
                    // Check if directory still exists (race condition handling)
                    // If it was created between validation and execution, handle gracefully
                    context.Logger.LogWarning("Directory already exists: {DirectoryPath}. This may have been created between validation and execution.", DirectoryPath);
                    return Task.FromResult(InstallationStepResult.FailureResult(
                        $"Directory already exists: {DirectoryPath}"));
                }
            }

            // Create parent directories if needed
            if (CreateParentDirectories)
            {
                CreateParentDirectoriesIfNeeded(context);
            }

            // Create the target directory
            context.Logger.LogDebug("Creating directory: {DirectoryPath}", DirectoryPath);
            Directory.CreateDirectory(DirectoryPath);
            _directoryCreated = true;
            context.Logger.LogInformation("Directory created successfully: {DirectoryPath}", DirectoryPath);

            // Set permissions if requested
            if (SetPermissions)
            {
                SetDirectoryPermissions(context);
            }

            return Task.FromResult(InstallationStepResult.SuccessResult(
                $"Successfully created directory: {DirectoryPath}"));
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "Failed to create directory");
            return Task.FromResult(InstallationStepResult.FailureResult(
                $"Directory creation failed: {ex.Message}",
                ex));
        }
    }

    /// <inheritdoc />
    public Task<InstallationStepResult> RollbackAsync(InstallationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Logger.LogInformation("Rolling back CreateDirectoryStep: {DirectoryPath}", DirectoryPath);

        try
        {
            // Restore permissions if they were changed
            if (_permissionsChanged && !string.IsNullOrEmpty(_originalPermissions))
            {
                try
                {
                    context.Logger.LogDebug("Restoring original permissions: {Permissions}", _originalPermissions);
                    RestorePermissions(_originalPermissions);
                    context.Logger.LogInformation("Original permissions restored");
                }
                catch (Exception ex)
                {
                    context.Logger.LogWarning(ex, "Failed to restore original permissions (best-effort continues)");
                }
            }

            // Remove directory if it was created by this step
            if (_directoryCreated && !_directoryExistedBefore)
            {
                if (Directory.Exists(DirectoryPath))
                {
                    // Only delete if directory is empty or only contains items we created
                    try
                    {
                        if (IsDirectoryEmpty(DirectoryPath))
                        {
                            context.Logger.LogDebug("Deleting created directory: {DirectoryPath}", DirectoryPath);
                            Directory.Delete(DirectoryPath, recursive: false);
                            context.Logger.LogInformation("Created directory deleted successfully");
                        }
                        else
                        {
                            context.Logger.LogWarning(
                                "Directory {DirectoryPath} is not empty, leaving in place",
                                DirectoryPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        context.Logger.LogWarning(ex, "Failed to delete directory (best-effort continues)");
                    }
                }
            }

            // Remove parent directories if they were created and are now empty
            RemoveCreatedParentDirectories(context);

            context.Logger.LogInformation("Rollback completed");
            return Task.FromResult(InstallationStepResult.SuccessResult("Rollback successful"));
        }
        catch (Exception ex)
        {
            context.Logger.LogWarning(ex, "Rollback encountered an error (best-effort continues)");
            return Task.FromResult(InstallationStepResult.FailureResult(
                $"Rollback failed: {ex.Message}",
                ex));
        }
    }

    /// <summary>
    /// Cleans up temporary resources created during execution.
    /// Called ALWAYS in finally block regardless of success/failure.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        // Clear all tracking state
        _createdParentDirectories.Clear();
        _originalPermissions = null;
        _directoryExistedBefore = false;
        _directoryCreated = false;
        _permissionsChanged = false;

        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    // Helper methods

    private void CreateParentDirectoriesIfNeeded(InstallationContext context)
    {
        var parentPath = Path.GetDirectoryName(DirectoryPath);
        if (string.IsNullOrEmpty(parentPath) || Directory.Exists(parentPath))
            return;

        // Build the list of parent directories to create
        var directoriesToCreate = new Stack<string>();
        var currentPath = parentPath;

        while (!string.IsNullOrEmpty(currentPath) && !Directory.Exists(currentPath))
        {
            directoriesToCreate.Push(currentPath);
            currentPath = Path.GetDirectoryName(currentPath);
        }

        // Create parent directories from top to bottom
        while (directoriesToCreate.Count > 0)
        {
            var dirToCreate = directoriesToCreate.Pop();

            // Only track if we actually create it
            bool existedBefore = Directory.Exists(dirToCreate);

            context.Logger.LogDebug("Creating parent directory: {DirectoryPath}", dirToCreate);
            Directory.CreateDirectory(dirToCreate);

            // Only track directories we actually created
            if (!existedBefore)
            {
                _createdParentDirectories.Add(dirToCreate);
                context.Logger.LogDebug("Parent directory created: {DirectoryPath}", dirToCreate);
            }
            else
            {
                context.Logger.LogDebug("Parent directory already existed: {DirectoryPath}", dirToCreate);
            }
        }
    }

    private void SetDirectoryPermissions(InstallationContext context)
    {
        if (string.IsNullOrWhiteSpace(Permissions))
            return;

        try
        {
            // Log warning about placeholder implementation
            context.Logger.LogWarning(
                "Permission operations are currently placeholder implementations and do not modify actual file system permissions. " +
                "This feature is planned for future implementation.");

            // Backup existing permissions if requested
            if (BackupExistingPermissions && _directoryExistedBefore)
            {
                _originalPermissions = GetCurrentPermissions();
                context.Logger.LogDebug("Backed up original permissions (placeholder): {Permissions}", _originalPermissions);
            }

            context.Logger.LogDebug("Setting permissions (placeholder): {Permissions}", Permissions);
            ApplyPermissions(Permissions);
            _permissionsChanged = true;
            context.Logger.LogInformation("Permissions set successfully (placeholder - no actual changes made)");
        }
        catch (Exception ex)
        {
            context.Logger.LogWarning(ex, "Failed to set permissions (continuing)");
            // Don't fail the entire operation for permission setting failures
        }
    }

    private void RemoveCreatedParentDirectories(InstallationContext context)
    {
        // Remove in reverse order (bottom to top)
        for (int i = _createdParentDirectories.Count - 1; i >= 0; i--)
        {
            var parentDir = _createdParentDirectories[i];
            try
            {
                if (Directory.Exists(parentDir) && IsDirectoryEmpty(parentDir))
                {
                    context.Logger.LogDebug("Removing empty parent directory: {DirectoryPath}", parentDir);
                    Directory.Delete(parentDir, recursive: false);
                    context.Logger.LogDebug("Parent directory removed: {DirectoryPath}", parentDir);
                }
                else
                {
                    context.Logger.LogDebug(
                        "Parent directory {DirectoryPath} is not empty or doesn't exist, stopping cleanup",
                        parentDir);
                    break; // Stop if we hit a non-empty directory
                }
            }
            catch (Exception ex)
            {
                context.Logger.LogWarning(ex, "Failed to remove parent directory (best-effort continues)");
                break; // Stop on error
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
            return false;
        }
    }

    private static int GetMaxPathLength()
    {
        return IsWindowsPlatform() ? 260 : 4096;
    }

    private static bool IsWindowsPlatform()
    {
        // Windows has a 260 character limit (unless long path support is enabled)
        // Unix systems have much larger limits (typically 4096)
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    }

    private static bool IsUnixPlatform()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
               RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    }

    private static bool ContainsInvalidPathCharacters(string path)
    {
        try
        {
            // Use Path.GetFullPath to validate the path
            Path.GetFullPath(path);
            return false;
        }
        catch (ArgumentException)
        {
            // Path contains invalid characters
            return true;
        }
        catch (System.Security.SecurityException)
        {
            // Caller does not have required permission
            return true;
        }
        catch (NotSupportedException)
        {
            // Path contains a colon in the middle (not a valid drive)
            return true;
        }
        catch (PathTooLongException)
        {
            // Path is too long
            return true;
        }
        catch
        {
            // Other exceptions
            return true;
        }
    }

    private static bool IsValidPermissionString(string? permissions)
    {
        if (string.IsNullOrWhiteSpace(permissions))
            return false;

        // Unix-style permission validation (e.g., "755", "644")
        if (IsUnixPlatform())
        {
            // Validate Unix octal permissions (e.g., 755)
            if (permissions.Length == 3 || permissions.Length == 4)
            {
                return permissions.All(c => c >= '0' && c <= '7');
            }
        }

        // Windows-style permission validation (simplified for now)
        if (IsWindowsPlatform())
        {
            // Accept common patterns like "EVERYONE:Full", "Users:Read", etc.
            return permissions.Contains(':') || permissions.Length <= 10;
        }

        return true; // Accept other formats for extensibility
    }

    /// <summary>
    /// Gets current permissions of the directory.
    /// WARNING: This is a placeholder implementation. It returns "default" instead of actual permissions.
    /// Actual permission reading is planned for future implementation.
    /// </summary>
    private string? GetCurrentPermissions()
    {
        try
        {
            if (!Directory.Exists(DirectoryPath))
                return null;

            if (IsUnixPlatform())
            {
                // PLACEHOLDER: On Unix, we could get permissions via file mode
                // In a real implementation, we'd use stat() system call or equivalent
                return "default";
            }
            else if (IsWindowsPlatform())
            {
                // PLACEHOLDER: On Windows, we could get ACLs
                // In a real implementation, we'd use GetSecurityInfo or FileSystemAccessRule
                return "default";
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Applies permissions to the directory.
    /// WARNING: This is a placeholder implementation. It does NOT actually modify file system permissions.
    /// Actual permission setting is planned for future implementation.
    /// </summary>
    private void ApplyPermissions(string permissions)
    {
        if (string.IsNullOrWhiteSpace(permissions))
            return;

        // PLACEHOLDER: Platform-specific permission application
        if (IsUnixPlatform())
        {
            // PLACEHOLDER: On Unix, we would use chmod
            // In a real implementation, we'd use P/Invoke to call chmod() or Process.Start to execute chmod command
            // Example: chmod(DirectoryPath, Convert.ToInt32(permissions, 8));
        }
        else if (IsWindowsPlatform())
        {
            // PLACEHOLDER: On Windows, we would use icacls or FileSystemAccessRule
            // In a real implementation, we'd use DirectorySecurity and FileSystemAccessRule classes
            // Example: var security = new DirectorySecurity(); security.SetAccessRule(...);
        }
    }

    /// <summary>
    /// Restores permissions to the directory.
    /// WARNING: This is a placeholder implementation. It does NOT actually modify file system permissions.
    /// Actual permission restoration is planned for future implementation.
    /// </summary>
    private void RestorePermissions(string permissions)
    {
        // PLACEHOLDER: Restore permissions using the same mechanism as ApplyPermissions
        ApplyPermissions(permissions);
    }
}
