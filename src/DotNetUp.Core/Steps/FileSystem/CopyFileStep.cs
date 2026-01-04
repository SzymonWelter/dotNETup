using DotNetUp.Core.Interfaces;
using DotNetUp.Core.Models;
using Microsoft.Extensions.Logging;

namespace DotNetUp.Core.Steps.FileSystem;

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
    public Task<InstallationResult> ValidateAsync(InstallationContext context)
    {
        context.Logger.LogDebug("Validating CopyFileStep: {Source} -> {Destination}", SourcePath, DestinationPath);

        try
        {
            // Check if source file exists
            if (!File.Exists(SourcePath))
            {
                return Task.FromResult(InstallationResult.FailureResult(
                    $"Source file does not exist: {SourcePath}"));
            }

            // Check if destination directory exists
            var destinationDir = Path.GetDirectoryName(DestinationPath);
            if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
            {
                return Task.FromResult(InstallationResult.FailureResult(
                    $"Destination directory does not exist: {destinationDir}"));
            }

            // Check if destination file exists and overwrite is not allowed
            if (File.Exists(DestinationPath) && !Overwrite)
            {
                return Task.FromResult(InstallationResult.FailureResult(
                    $"Destination file already exists and overwrite is not enabled: {DestinationPath}"));
            }

            // Check read permission on source
            try
            {
                using var stream = File.OpenRead(SourcePath);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Task.FromResult(InstallationResult.FailureResult(
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
                    return Task.FromResult(InstallationResult.FailureResult(
                        $"No write permission for destination directory: {destinationDir}",
                        ex));
                }
            }

            context.Logger.LogDebug("Validation successful for CopyFileStep");
            return Task.FromResult(InstallationResult.SuccessResult("Validation successful"));
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "Unexpected error during validation");
            return Task.FromResult(InstallationResult.FailureResult(
                "Unexpected error during validation",
                ex));
        }
    }

    /// <inheritdoc />
    public Task<InstallationResult> ExecuteAsync(InstallationContext context)
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
            }

            // Copy the file
            context.Logger.LogDebug("Copying file...");
            File.Copy(SourcePath, DestinationPath, overwrite: Overwrite);

            context.Logger.LogInformation("File copied successfully");
            return Task.FromResult(InstallationResult.SuccessResult(
                $"Successfully copied '{SourcePath}' to '{DestinationPath}'"));
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "Failed to copy file");

            // Clean up backup if it was created
            if (_backupPath != null && File.Exists(_backupPath))
            {
                try
                {
                    File.Delete(_backupPath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }

            return Task.FromResult(InstallationResult.FailureResult(
                $"Failed to copy file: {ex.Message}",
                ex));
        }
    }

    /// <inheritdoc />
    public Task<InstallationResult> RollbackAsync(InstallationContext context)
    {
        context.Logger.LogInformation("Rolling back CopyFileStep: {Destination}", DestinationPath);

        try
        {
            // If we created a backup, restore it
            if (_backupPath != null && File.Exists(_backupPath))
            {
                context.Logger.LogDebug("Restoring backup from: {BackupPath}", _backupPath);
                File.Copy(_backupPath, DestinationPath, overwrite: true);
                File.Delete(_backupPath);
                context.Logger.LogInformation("Backup restored successfully");
            }
            // If destination didn't exist before and we created it, delete it
            else if (!_destinationExistedBefore && File.Exists(DestinationPath))
            {
                context.Logger.LogDebug("Deleting newly created file: {Destination}", DestinationPath);
                File.Delete(DestinationPath);
                context.Logger.LogInformation("Newly created file deleted successfully");
            }
            else
            {
                context.Logger.LogDebug("No rollback action needed");
            }

            return Task.FromResult(InstallationResult.SuccessResult("Rollback successful"));
        }
        catch (Exception ex)
        {
            // Best-effort rollback: log the error but return success
            // The executor will log this as a warning and continue with other rollbacks
            context.Logger.LogWarning(ex, "Rollback encountered an error (best-effort continues)");
            return Task.FromResult(InstallationResult.FailureResult(
                $"Rollback failed: {ex.Message}",
                ex));
        }
    }
}
