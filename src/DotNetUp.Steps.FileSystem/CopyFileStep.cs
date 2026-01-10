using DotNetUp.Core.Interfaces;
using DotNetUp.Core.Models;
using Microsoft.Extensions.Logging;

namespace DotNetUp.Steps.FileSystem;

/// <summary>
/// Installation step that copies a file from source to destination.
/// Supports overwrite mode with automatic backup and rollback.
/// </summary>
public class CopyFileStep : IInstallationStep
{
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
    /// If true and destination exists, a backup is created for rollback.
    /// </summary>
    public bool Overwrite { get; }

    private string? _backupPath;
    private bool _destinationExistedBefore;
    private bool _copySucceeded;

    /// <summary>
    /// Creates a new copy file step.
    /// </summary>
    /// <param name="sourcePath">Path to the source file</param>
    /// <param name="destinationPath">Path to the destination file</param>
    /// <param name="overwrite">Whether to overwrite if destination exists</param>
    /// <exception cref="ArgumentException">Thrown when paths are null or whitespace</exception>
    public CopyFileStep(string sourcePath, string destinationPath, bool overwrite = false)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path cannot be null or empty", nameof(sourcePath));

        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("Destination path cannot be null or empty", nameof(destinationPath));

        SourcePath = sourcePath;
        DestinationPath = destinationPath;
        Overwrite = overwrite;
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
                    $"Source file does not exist: {SourcePath}"));
            }

            // Check if destination directory exists
            var destinationDir = Path.GetDirectoryName(DestinationPath);
            if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
            {
                return Task.FromResult(InstallationStepResult.FailureResult(
                    $"Destination directory does not exist: {destinationDir}"));
            }

            // Check if destination file exists and overwrite is not allowed
            if (File.Exists(DestinationPath) && !Overwrite)
            {
                return Task.FromResult(InstallationStepResult.FailureResult(
                    $"Destination file already exists and overwrite is not enabled: {DestinationPath}"));
            }

            // Check read permission on source
            try
            {
                using var stream = File.OpenRead(SourcePath);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Task.FromResult(InstallationStepResult.FailureResult(
                    $"No read permission for source file: {SourcePath}",
                    ex));
            }

            // Check write permission on destination directory
            if (!string.IsNullOrEmpty(destinationDir))
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
                        $"No write permission for destination directory: {destinationDir}",
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

        try
        {
            // Check if destination exists
            _destinationExistedBefore = File.Exists(DestinationPath);

            // If destination exists and overwrite is enabled, create backup
            if (_destinationExistedBefore && Overwrite)
            {
                _backupPath = $"{DestinationPath}.backup_{Guid.NewGuid()}";
                context.Logger.LogDebug("Backing up existing file to: {BackupPath}", _backupPath);
                File.Copy(DestinationPath, _backupPath, overwrite: false);
                context.Logger.LogDebug("Backup created successfully");
            }

            // Copy the file
            context.Logger.LogDebug("Copying file...");
            File.Copy(SourcePath, DestinationPath, overwrite: Overwrite);

            // Mark copy as successful
            _copySucceeded = true;

            context.Logger.LogInformation("File copied successfully");
            return Task.FromResult(InstallationStepResult.SuccessResult(
                $"Successfully copied '{SourcePath}' to '{DestinationPath}'"));
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "Failed to copy file, original file will be restored");

            return Task.FromResult(InstallationStepResult.FailureResult(
                $"Failed to copy file: {ex.Message}",
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
                // If copy failed, restore original file to prevent data corruption
                // This handles ContinueOnError scenarios where rollback wasn't called
                if (!_copySucceeded && _destinationExistedBefore)
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
}
