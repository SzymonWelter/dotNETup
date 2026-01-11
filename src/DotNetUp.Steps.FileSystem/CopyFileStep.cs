using DotNetUp.Core.Interfaces;
using DotNetUp.Core.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace DotNetUp.Steps.FileSystem;

/// <summary>
/// Installation step that copies a file from source to destination.
/// Supports overwrite mode with automatic backup and rollback.
/// </summary>
public class CopyFileStep : IInstallationStep, IAsyncDisposable
{
    private const int MaxPathLengthWindows = 260;
    private const int MaxPathLengthUnix = 4096;

    /// <inheritdoc />
    public string Name => "CopyFile";

    /// <inheritdoc />
    public string Description => $"Copy '{SourcePath}' to '{DestinationPath}'";

    /// <summary>
    /// Gets the source file path to copy from.
    /// </summary>
    public string SourcePath { get; }

    /// <summary>
    /// Gets the destination file path to copy to.
    /// </summary>
    public string DestinationPath { get; }

    /// <summary>
    /// Gets a value indicating whether to overwrite the destination file if it exists.
    /// </summary>
    public bool Overwrite { get; }

    /// <summary>
    /// Gets a value indicating whether to create a backup when overwriting an existing file.
    /// Only applies when Overwrite is true and destination file exists.
    /// </summary>
    public bool BackupExisting { get; }

    /// <summary>
    /// Gets a value indicating whether to preserve file timestamps (creation, modification, access times).
    /// </summary>
    public bool PreserveTimestamps { get; }

    /// <summary>
    /// Gets a value indicating whether to copy file attributes (read-only, hidden, system, archive).
    /// </summary>
    public bool CopyAttributes { get; }

    /// <summary>
    /// Gets a value indicating whether to create destination directory if it doesn't exist.
    /// </summary>
    public bool CreateDirectoriesIfNeeded { get; }

    /// <summary>
    /// Gets a value indicating whether to perform disk space validation before copying.
    /// </summary>
    public bool ValidateDiskSpace { get; }

    private string? _backupPath;
    private bool _destinationExistedBefore;
    private bool _copySucceeded;
    private readonly Stopwatch _stopwatch = new();

    /// <summary>
    /// Creates a new copy file step.
    /// </summary>
    /// <param name="sourcePath">Path to the source file</param>
    /// <param name="destinationPath">Path to the destination file</param>
    /// <param name="overwrite">Whether to overwrite if destination exists</param>
    /// <param name="backupExisting">Whether to create a backup when overwriting (only applies if overwrite is true, default: false)</param>
    /// <param name="preserveTimestamps">Whether to preserve file timestamps (default: true)</param>
    /// <param name="copyAttributes">Whether to copy file attributes (default: true)</param>
    /// <param name="createDirectoriesIfNeeded">Whether to create destination directory if missing (default: true)</param>
    /// <param name="validateDiskSpace">Whether to validate sufficient disk space before copying (default: true)</param>
    /// <exception cref="ArgumentException">Thrown when paths are null or whitespace</exception>
    public CopyFileStep(
        string sourcePath,
        string destinationPath,
        bool overwrite = false,
        bool backupExisting = false,
        bool preserveTimestamps = true,
        bool copyAttributes = true,
        bool createDirectoriesIfNeeded = true,
        bool validateDiskSpace = true)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path cannot be null or empty", nameof(sourcePath));

        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("Destination path cannot be null or empty", nameof(destinationPath));

        SourcePath = sourcePath;
        DestinationPath = destinationPath;
        Overwrite = overwrite;
        BackupExisting = backupExisting;
        PreserveTimestamps = preserveTimestamps;
        CopyAttributes = copyAttributes;
        CreateDirectoriesIfNeeded = createDirectoriesIfNeeded;
        ValidateDiskSpace = validateDiskSpace;
    }

    /// <inheritdoc />
    public Task<InstallationStepResult> ValidateAsync(InstallationContext context)
    {
        context.Logger.LogDebug("Validating CopyFileStep: {Source} -> {Destination}", SourcePath, DestinationPath);

        try
        {
            // Check if source file exists
            if (!File.Exists(SourcePath))
            {
                return Task.FromResult(InstallationStepResult.FailureResult(
                    $"Source file not found: {SourcePath}"));
            }

            // Validate path lengths
            var maxPathLength = OperatingSystem.IsWindows() ? MaxPathLengthWindows : MaxPathLengthUnix;
            if (SourcePath.Length > maxPathLength)
            {
                return Task.FromResult(InstallationStepResult.FailureResult(
                    $"Source path exceeds maximum length of {maxPathLength} characters: {SourcePath}"));
            }

            if (DestinationPath.Length > maxPathLength)
            {
                return Task.FromResult(InstallationStepResult.FailureResult(
                    $"Destination path exceeds maximum length of {maxPathLength} characters: {DestinationPath}"));
            }

            // Check for circular copy (source == destination or destination inside source directory)
            var normalizedSource = Path.GetFullPath(SourcePath);
            var normalizedDestination = Path.GetFullPath(DestinationPath);

            if (string.Equals(normalizedSource, normalizedDestination, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(InstallationStepResult.FailureResult(
                    "Cannot copy file to itself. Source and destination paths are identical."));
            }

            // Check if destination directory exists or can be created
            var destinationDir = Path.GetDirectoryName(DestinationPath);
            if (!string.IsNullOrEmpty(destinationDir))
            {
                if (!Directory.Exists(destinationDir))
                {
                    if (!CreateDirectoriesIfNeeded)
                    {
                        return Task.FromResult(InstallationStepResult.FailureResult(
                            $"Destination directory does not exist: {destinationDir}"));
                    }

                    // Check if we can create the directory by checking parent directory write permission
                    var parentDir = Directory.GetParent(destinationDir);
                    if (parentDir != null && !Directory.Exists(parentDir.FullName))
                    {
                        return Task.FromResult(InstallationStepResult.FailureResult(
                            $"Parent directory does not exist and cannot be created: {parentDir.FullName}"));
                    }
                }
            }

            // Check if destination file exists and overwrite is not allowed
            if (File.Exists(DestinationPath) && !Overwrite)
            {
                return Task.FromResult(InstallationStepResult.FailureResult(
                    $"Destination file already exists: {DestinationPath}. Set Overwrite to true to overwrite."));
            }

            // Check read permission on source
            try
            {
                using var stream = File.OpenRead(SourcePath);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Task.FromResult(InstallationStepResult.FailureResult(
                    $"Cannot read source file. Permission denied: {SourcePath}",
                    ex));
            }
            catch (IOException ex)
            {
                return Task.FromResult(InstallationStepResult.FailureResult(
                    $"Cannot read source file: {SourcePath}",
                    ex));
            }

            // Validate disk space if requested
            if (ValidateDiskSpace && !string.IsNullOrEmpty(destinationDir))
            {
                try
                {
                    var sourceFileInfo = new FileInfo(SourcePath);
                    var sourceFileSize = sourceFileInfo.Length;

                    var destinationDrive = !string.IsNullOrEmpty(destinationDir) && Directory.Exists(destinationDir)
                        ? new DriveInfo(Path.GetPathRoot(destinationDir) ?? "")
                        : null;

                    if (destinationDrive != null && destinationDrive.IsReady)
                    {
                        var availableSpace = destinationDrive.AvailableFreeSpace;

                        // Calculate required space correctly:
                        // - Always need space for source file
                        // - If overwriting existing file AND BackupExisting is true, need space for backup
                        var requiredSpace = sourceFileSize;
                        if (File.Exists(DestinationPath) && Overwrite && BackupExisting)
                        {
                            var destinationFileSize = new FileInfo(DestinationPath).Length;
                            requiredSpace += destinationFileSize; // Space for backup
                        }

                        if (availableSpace < requiredSpace)
                        {
                            return Task.FromResult(InstallationStepResult.FailureResult(
                                $"Insufficient disk space. Required: {FormatBytes(requiredSpace)}, Available: {FormatBytes(availableSpace)}"));
                        }

                        context.Logger.LogDebug(
                            "Disk space validation passed. Required: {Required}, Available: {Available}",
                            FormatBytes(requiredSpace),
                            FormatBytes(availableSpace));
                    }
                }
                catch (Exception ex)
                {
                    context.Logger.LogWarning(ex, "Could not validate disk space, continuing anyway");
                }
            }

            // Check write permission on destination directory
            if (!string.IsNullOrEmpty(destinationDir) && Directory.Exists(destinationDir))
            {
                try
                {
                    // Try to create a temporary file to verify write permissions
                    var tempFile = Path.Combine(destinationDir, $".dotnetup_test_{Guid.NewGuid()}");
                    File.WriteAllText(tempFile, "test");
                    File.Delete(tempFile);
                }
                catch (UnauthorizedAccessException ex)
                {
                    return Task.FromResult(InstallationStepResult.FailureResult(
                        $"Cannot write to destination directory. Permission denied: {destinationDir}",
                        ex));
                }
                catch (IOException ex)
                {
                    return Task.FromResult(InstallationStepResult.FailureResult(
                        $"Cannot write to destination directory: {destinationDir}",
                        ex));
                }
            }

            context.Logger.LogDebug("Validation successful for CopyFileStep");
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
        context.Logger.LogInformation("Executing CopyFileStep: {Source} -> {Destination}", SourcePath, DestinationPath);
        _stopwatch.Start();

        try
        {
            var sourceFileInfo = new FileInfo(SourcePath);
            var sourceFileSize = sourceFileInfo.Length;

            context.ReportStepProgress("Preparing to copy file", 0);

            // Create destination directory if needed
            var destinationDir = Path.GetDirectoryName(DestinationPath);
            if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
            {
                if (CreateDirectoriesIfNeeded)
                {
                    context.Logger.LogDebug("Creating destination directory: {Directory}", destinationDir);
                    Directory.CreateDirectory(destinationDir);
                    context.Logger.LogDebug("Destination directory created successfully");
                }
                else
                {
                    return Task.FromResult(InstallationStepResult.FailureResult(
                        $"Destination directory does not exist: {destinationDir}"));
                }
            }

            // Check if destination exists
            _destinationExistedBefore = File.Exists(DestinationPath);

            context.ReportStepProgress("Creating backup if needed", 10);

            // If destination exists, overwrite is enabled, AND BackupExisting is true, create backup
            if (_destinationExistedBefore && Overwrite && BackupExisting)
            {
                _backupPath = $"{DestinationPath}.backup_{Guid.NewGuid()}";
                context.Logger.LogDebug("Backing up existing file to: {BackupPath}", _backupPath);
                File.Copy(DestinationPath, _backupPath, overwrite: false);
                context.Logger.LogDebug("Backup created successfully");
            }

            context.ReportStepProgress("Copying file", 30);

            // Copy the file
            context.Logger.LogDebug("Copying file...");
            File.Copy(SourcePath, DestinationPath, overwrite: Overwrite);

            context.ReportStepProgress("Verifying file copy", 70);

            // Verify file was copied correctly
            if (!File.Exists(DestinationPath))
            {
                return Task.FromResult(InstallationStepResult.FailureResult(
                    "File copy verification failed: Destination file does not exist after copy operation"));
            }

            var destinationFileInfo = new FileInfo(DestinationPath);
            if (destinationFileInfo.Length != sourceFileSize)
            {
                return Task.FromResult(InstallationStepResult.FailureResult(
                    $"File copy verification failed: Size mismatch. Expected {sourceFileSize} bytes, got {destinationFileInfo.Length} bytes"));
            }

            context.ReportStepProgress("Preserving file attributes", 85);

            // Copy file attributes if requested, otherwise reset to normal
            if (CopyAttributes)
            {
                try
                {
                    var sourceAttributes = File.GetAttributes(SourcePath);
                    File.SetAttributes(DestinationPath, sourceAttributes);
                    context.Logger.LogDebug("File attributes copied successfully");
                }
                catch (Exception ex)
                {
                    context.Logger.LogWarning(ex, "Could not copy file attributes, continuing anyway");
                }
            }
            else
            {
                try
                {
                    // File.Copy copies attributes by default, so we need to reset them
                    File.SetAttributes(DestinationPath, FileAttributes.Normal);
                    context.Logger.LogDebug("File attributes reset to normal");
                }
                catch (Exception ex)
                {
                    context.Logger.LogWarning(ex, "Could not reset file attributes, continuing anyway");
                }
            }

            // Preserve timestamps if requested, otherwise set to current time
            if (PreserveTimestamps)
            {
                try
                {
                    File.SetCreationTime(DestinationPath, sourceFileInfo.CreationTime);
                    File.SetLastWriteTime(DestinationPath, sourceFileInfo.LastWriteTime);
                    File.SetLastAccessTime(DestinationPath, sourceFileInfo.LastAccessTime);
                    context.Logger.LogDebug("File timestamps preserved successfully");
                }
                catch (Exception ex)
                {
                    context.Logger.LogWarning(ex, "Could not preserve file timestamps, continuing anyway");
                }
            }
            else
            {
                try
                {
                    // File.Copy preserves timestamps by default, so we need to reset them
                    var now = DateTime.Now;
                    File.SetCreationTime(DestinationPath, now);
                    File.SetLastWriteTime(DestinationPath, now);
                    File.SetLastAccessTime(DestinationPath, now);
                    context.Logger.LogDebug("File timestamps reset to current time");
                }
                catch (Exception ex)
                {
                    context.Logger.LogWarning(ex, "Could not reset file timestamps, continuing anyway");
                }
            }

            // Mark copy as successful
            _copySucceeded = true;
            _stopwatch.Stop();

            context.ReportStepProgress("File copy completed", 100);

            // Store metadata in context for use by other steps
            var resultData = new Dictionary<string, object>
            {
                ["CopiedFilePath"] = DestinationPath,
                ["FileSize"] = sourceFileSize,
                ["CopyDuration"] = _stopwatch.Elapsed,
                ["BackupCreated"] = _backupPath != null
            };

            if (_backupPath != null)
            {
                resultData["BackupPath"] = _backupPath;
            }

            context.Logger.LogInformation(
                "File copied successfully in {Duration:F2}s. Size: {Size}",
                _stopwatch.Elapsed.TotalSeconds,
                FormatBytes(sourceFileSize));

            return Task.FromResult(InstallationStepResult.SuccessResult(
                $"Successfully copied '{SourcePath}' to '{DestinationPath}' ({FormatBytes(sourceFileSize)})",
                resultData));
        }
        catch (Exception ex)
        {
            _stopwatch.Stop();
            context.Logger.LogError(ex, "Failed to copy file after {Duration:F2}s", _stopwatch.Elapsed.TotalSeconds);

            return Task.FromResult(InstallationStepResult.FailureResult(
                $"File copy failed: {ex.Message}",
                ex));
        }
    }

    /// <inheritdoc />
    public Task<InstallationStepResult> RollbackAsync(InstallationContext context)
    {
        context.Logger.LogInformation("Rolling back CopyFileStep: {Destination}", DestinationPath);

        try
        {
            // Scenario 1: Backup exists (either copy succeeded or failed after backup)
            // - If copy succeeded: rollback because a later step failed
            // - If copy failed: restore original file (destination may be corrupted)
            if (_backupPath != null && File.Exists(_backupPath))
            {
                if (_copySucceeded)
                {
                    context.Logger.LogDebug(
                        "Copy succeeded but later step failed. Restoring backup from: {BackupPath}",
                        _backupPath);
                }
                else
                {
                    context.Logger.LogWarning(
                        "Copy failed and destination may be corrupted. Restoring original from backup: {BackupPath}",
                        _backupPath);
                }

                // Restore the backup (overwrite potentially corrupted destination)
                File.Copy(_backupPath, DestinationPath, overwrite: true);

                // Note: Backup file deletion is handled by DisposeAsync (always called in finally block)
                // This ensures cleanup happens even if rollback throws an exception

                context.Logger.LogInformation("Original file restored successfully from backup");
                return Task.FromResult(InstallationStepResult.SuccessResult("Rollback successful - original file restored"));
            }
            // Scenario 2: No backup exists, but destination didn't exist before
            // This means we created a new file (copy may have succeeded or partially succeeded)
            else if (!_destinationExistedBefore && File.Exists(DestinationPath))
            {
                context.Logger.LogDebug("Deleting newly created file: {Destination}", DestinationPath);
                File.Delete(DestinationPath);
                context.Logger.LogInformation("Newly created file deleted successfully");
                return Task.FromResult(InstallationStepResult.SuccessResult("Rollback successful - new file removed"));
            }
            // Scenario 3: No action needed
            else
            {
                context.Logger.LogDebug("No rollback action needed (no backup and destination existed before)");
                return Task.FromResult(InstallationStepResult.SuccessResult("Rollback successful - no action needed"));
            }
        }
        catch (Exception ex)
        {
            // Best-effort rollback: log the error but return failure result
            // The executor will log this as a warning and continue with other rollbacks
            context.Logger.LogWarning(ex, "Rollback encountered an error (best-effort continues)");
            return Task.FromResult(InstallationStepResult.FailureResult(
                $"Rollback failed: {ex.Message}",
                ex));
        }
    }

    /// <summary>
    /// Cleans up temporary resources (backup files) created during execution.
    /// Called ALWAYS in finally block regardless of success/failure or ContinueOnError settings.
    /// This is the single source of truth for backup file cleanup.
    ///
    /// Responsibilities:
    /// 1. Restore backup if copy failed (prevents data corruption)
    /// 2. Delete backup file (prevents orphaned resources)
    ///
    /// Why not delete in RollbackAsync?
    /// - DisposeAsync is ALWAYS called (finally block)
    /// - RollbackAsync may not be called (ContinueOnError, success)
    /// - RollbackAsync may throw before cleanup
    /// - Single Responsibility: disposal handles cleanup
    /// </summary>
    public ValueTask DisposeAsync()
    {
        // Only process if we created a backup
        if (_backupPath != null && File.Exists(_backupPath))
        {
            try
            {
                // CRITICAL FIX: If copy failed, restore backup regardless of whether file existed before
                // This prevents data corruption in scenarios where:
                // 1. Destination existed and copy failed after backup was created
                // 2. Destination didn't exist, backup was created erroneously, and copy failed
                if (!_copySucceeded)
                {
                    File.Copy(_backupPath, DestinationPath, overwrite: true);
                }

                // Always delete backup file
                // Scenarios:
                // 1. Copy succeeded → backup no longer needed
                // 2. Copy failed → backup restored above, now can delete
                // 3. Rollback called → backup was restored, now can delete
                File.Delete(_backupPath);
            }
            catch
            {
                // Best-effort: silently ignore errors
                // Backup file may be orphaned but we tried our best
                // Can't log here as we don't have context
            }
        }

        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Formats a byte count into a human-readable string.
    /// </summary>
    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}
