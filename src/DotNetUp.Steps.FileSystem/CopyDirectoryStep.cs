using DotNetUp.Core.Interfaces;
using DotNetUp.Core.Models;
using Microsoft.Extensions.Logging;

namespace DotNetUp.Steps.FileSystem;

/// <summary>
/// Installation step that copies a directory and its contents from source to destination.
/// Supports recursive copying, filtering, attribute preservation, backup creation, and complete rollback.
/// </summary>
public class CopyDirectoryStep : IInstallationStep
{
    /// <inheritdoc />
    public string Name => "CopyDirectory";

    /// <inheritdoc />
    public string Description => $"Copy directory '{SourcePath}' to '{DestinationPath}'";

    /// <summary>
    /// Gets the source directory path to copy from.
    /// </summary>
    public string SourcePath { get; }

    /// <summary>
    /// Gets the destination directory path to copy to.
    /// </summary>
    public string DestinationPath { get; }

    /// <summary>
    /// Gets the copy options controlling filtering, overwrite, and preservation behavior.
    /// </summary>
    public CopyDirectoryOptions Options { get; }

    private readonly DirectoryCopyMetadata _metadata = new();
    private readonly DirectoryEnumerationFilter _filter;
    private bool _copySucceeded;

    /// <summary>
    /// Creates a new copy directory step.
    /// </summary>
    /// <param name="sourcePath">Path to the source directory</param>
    /// <param name="destinationPath">Path to the destination directory</param>
    /// <param name="options">Copy options, or null to use defaults</param>
    /// <exception cref="ArgumentException">Thrown when paths are null or whitespace</exception>
    public CopyDirectoryStep(string sourcePath, string destinationPath, CopyDirectoryOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path cannot be null or empty", nameof(sourcePath));

        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("Destination path cannot be null or empty", nameof(destinationPath));

        SourcePath = sourcePath;
        DestinationPath = destinationPath;
        Options = options ?? new CopyDirectoryOptions();

        // Validate options
        var (isValid, errorMessage) = Options.Validate();
        if (!isValid)
            throw new ArgumentException($"Invalid options: {errorMessage}", nameof(options));

        _filter = new DirectoryEnumerationFilter(Options);
    }

    /// <inheritdoc />
    public Task<InstallationStepResult> ValidateAsync(InstallationContext context)
    {
        context.Logger.LogDebug("Validating CopyDirectoryStep: {Source} -> {Destination}", SourcePath, DestinationPath);

        try
        {
            // Check if source directory exists
            if (!Directory.Exists(SourcePath))
            {
                return Task.FromResult(InstallationStepResult.FailureResult(
                    $"Source directory does not exist: {SourcePath}"));
            }

            // Check if source and destination are the same
            var normalizedSource = Path.GetFullPath(SourcePath);
            var normalizedDestination = Path.GetFullPath(DestinationPath);

            if (string.Equals(normalizedSource, normalizedDestination, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(InstallationStepResult.FailureResult(
                    "Source and destination directories cannot be the same"));
            }

            // Check if destination is a subdirectory of source (would cause infinite recursion)
            if (normalizedDestination.StartsWith(normalizedSource + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(InstallationStepResult.FailureResult(
                    "Destination directory cannot be a subdirectory of source directory"));
            }

            // Check read permission on source directory
            try
            {
                _ = Directory.EnumerateFileSystemEntries(SourcePath).Take(1).ToList();
            }
            catch (UnauthorizedAccessException ex)
            {
                return Task.FromResult(InstallationStepResult.FailureResult(
                    $"No read permission for source directory: {SourcePath}",
                    ex));
            }

            // Check if destination directory exists when CreateDestinationIfMissing is false
            if (!Options.CreateDestinationIfMissing && !Directory.Exists(DestinationPath))
            {
                return Task.FromResult(InstallationStepResult.FailureResult(
                    $"Destination directory does not exist and CreateDestinationIfMissing is false: {DestinationPath}"));
            }

            // Check if destination parent directory exists
            var destinationParent = Path.GetDirectoryName(DestinationPath);
            if (!string.IsNullOrEmpty(destinationParent) && !Directory.Exists(destinationParent))
            {
                return Task.FromResult(InstallationStepResult.FailureResult(
                    $"Destination parent directory does not exist: {destinationParent}"));
            }

            // Check write permission on destination parent (or destination itself if it exists)
            var pathToCheck = Directory.Exists(DestinationPath) ? DestinationPath : destinationParent;
            if (!string.IsNullOrEmpty(pathToCheck))
            {
                try
                {
                    var tempFile = Path.Combine(pathToCheck, $".dotnetup_test_{Guid.NewGuid()}");
                    File.WriteAllText(tempFile, "test");
                    File.Delete(tempFile);
                }
                catch (UnauthorizedAccessException ex)
                {
                    return Task.FromResult(InstallationStepResult.FailureResult(
                        $"No write permission for destination: {pathToCheck}",
                        ex));
                }
            }

            // Check if destination exists and overwrite is not allowed
            if (Directory.Exists(DestinationPath) && !Options.Overwrite)
            {
                // Check if destination has any files that would conflict
                var hasConflicts = EnumerateFilesToCopy(context).Any(fileInfo =>
                {
                    var relativePath = Path.GetRelativePath(SourcePath, fileInfo.FullName);
                    var destFile = Path.Combine(DestinationPath, relativePath);
                    return File.Exists(destFile);
                });

                if (hasConflicts)
                {
                    return Task.FromResult(InstallationStepResult.FailureResult(
                        $"Destination directory exists with conflicting files and overwrite is not enabled: {DestinationPath}"));
                }
            }

            // Check available disk space
            try
            {
                var driveInfo = new DriveInfo(Path.GetPathRoot(normalizedDestination)!);
                var estimatedSize = EstimateDirectorySize(context);

                if (driveInfo.AvailableFreeSpace < estimatedSize)
                {
                    return Task.FromResult(InstallationStepResult.FailureResult(
                        $"Insufficient disk space. Required: {estimatedSize:N0} bytes, Available: {driveInfo.AvailableFreeSpace:N0} bytes"));
                }
            }
            catch (Exception ex)
            {
                // Disk space check is best-effort, don't fail validation if we can't check
                context.Logger.LogWarning(ex, "Could not check disk space, proceeding anyway");
            }

            context.Logger.LogDebug("Validation successful for CopyDirectoryStep");
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
        context.Logger.LogInformation("Executing CopyDirectoryStep: {Source} -> {Destination}", SourcePath, DestinationPath);
        context.Logger.LogDebug("Copy options: {Options}", Options);

        var startTime = DateTime.UtcNow;

        try
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            // Create destination directory if it doesn't exist (and option allows it)
            if (!Directory.Exists(DestinationPath))
            {
                if (Options.CreateDestinationIfMissing)
                {
                    context.Logger.LogDebug("Creating destination directory: {Destination}", DestinationPath);
                    Directory.CreateDirectory(DestinationPath);
                    _metadata.RecordCreatedDirectory(DestinationPath);
                }
                else
                {
                    return Task.FromResult(InstallationStepResult.FailureResult(
                        $"Destination directory does not exist and CreateDestinationIfMissing is false: {DestinationPath}"));
                }
            }

            // Enumerate all directories and files to copy
            var filesToCopy = EnumerateFilesToCopy(context).ToList();
            var directoriesToCreate = EnumerateDirectoriesToCreate(context).ToList();
            var totalFiles = filesToCopy.Count;

            context.Logger.LogInformation("Found {Count} files to copy and {DirCount} directories to create",
                totalFiles, directoriesToCreate.Count);
            context.ReportStepProgress("Preparing to copy files", 0);

            // Create all directories first to preserve directory structure
            foreach (var sourceDir in directoriesToCreate)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                var relativePath = Path.GetRelativePath(SourcePath, sourceDir.FullName);
                var destDir = Path.Combine(DestinationPath, relativePath);

                if (!Directory.Exists(destDir))
                {
                    context.Logger.LogTrace("Creating directory: {Directory}", destDir);
                    Directory.CreateDirectory(destDir);
                    _metadata.RecordCreatedDirectory(destDir);
                }
            }

            if (totalFiles == 0)
            {
                context.Logger.LogInformation("No files to copy");
                _copySucceeded = true;

                // Record copy duration even for no files case
                var duration = DateTime.UtcNow - startTime;
                _metadata.SetCopyDuration(duration);

                // Store properties in context
                context.Properties["FilesCopied"] = 0;
                context.Properties["DirectoriesCreated"] = _metadata.TotalDirectoriesCreated;
                context.Properties["TotalBytesCopied"] = 0L;
                context.Properties["CopyDuration"] = _metadata.CopyDuration ?? TimeSpan.Zero;

                return Task.FromResult(InstallationStepResult.SuccessResult(
                    $"No files to copy from '{SourcePath}'",
                    new Dictionary<string, object>
                    {
                        ["Metadata"] = _metadata,
                        ["FilesCopied"] = 0,
                        ["DirectoriesCreated"] = _metadata.TotalDirectoriesCreated,
                        ["TotalBytesCopied"] = 0L,
                        ["CopyDuration"] = _metadata.CopyDuration ?? TimeSpan.Zero
                    }));
            }

            // Copy files
            var filesProcessed = 0;
            foreach (var sourceFile in filesToCopy)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                var relativePath = Path.GetRelativePath(SourcePath, sourceFile.FullName);
                var destFile = Path.Combine(DestinationPath, relativePath);

                CopyFile(sourceFile, destFile, context);

                filesProcessed++;

                // Report progress periodically
                if (filesProcessed % Options.ProgressInterval == 0 || filesProcessed == totalFiles)
                {
                    var percentComplete = (int)((double)filesProcessed / totalFiles * 100);
                    context.ReportStepProgress($"Copied {filesProcessed}/{totalFiles} files", percentComplete);
                }
            }

            _copySucceeded = true;

            // Record copy duration
            var copyDuration = DateTime.UtcNow - startTime;
            _metadata.SetCopyDuration(copyDuration);

            context.Logger.LogInformation(
                "Directory copied successfully. {Metadata}",
                _metadata);

            // Store metadata in context for potential use by other steps
            context.Properties[$"{Name}_Metadata"] = _metadata;
            context.Properties["FilesCopied"] = _metadata.TotalFilesProcessed;
            context.Properties["DirectoriesCreated"] = _metadata.TotalDirectoriesCreated;
            context.Properties["TotalBytesCopied"] = _metadata.TotalBytesCopied;
            context.Properties["CopyDuration"] = _metadata.CopyDuration ?? TimeSpan.Zero;

            return Task.FromResult(InstallationStepResult.SuccessResult(
                $"Successfully copied {filesProcessed} files from '{SourcePath}' to '{DestinationPath}'",
                new Dictionary<string, object>
                {
                    ["Metadata"] = _metadata,
                    ["FilesCopied"] = _metadata.TotalFilesProcessed,
                    ["DirectoriesCreated"] = _metadata.TotalDirectoriesCreated,
                    ["TotalBytesCopied"] = _metadata.TotalBytesCopied,
                    ["CopyDuration"] = _metadata.CopyDuration ?? TimeSpan.Zero
                }));
        }
        catch (OperationCanceledException)
        {
            context.Logger.LogWarning("Directory copy operation was cancelled");
            return Task.FromResult(InstallationStepResult.FailureResult("Operation was cancelled"));
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "Failed to copy directory");
            return Task.FromResult(InstallationStepResult.FailureResult(
                $"Failed to copy directory: {ex.Message}",
                ex));
        }
    }

    /// <inheritdoc />
    public Task<InstallationStepResult> RollbackAsync(InstallationContext context)
    {
        context.Logger.LogInformation("Rolling back CopyDirectoryStep: {Destination}", DestinationPath);

        var errors = new List<string>();
        var successCount = 0;

        try
        {
            // Restore backups first (files that were overwritten)
            foreach (var (destFile, backupFile) in _metadata.BackupPaths)
            {
                try
                {
                    if (File.Exists(backupFile))
                    {
                        context.Logger.LogDebug("Restoring backup: {Backup} -> {Destination}", backupFile, destFile);
                        File.Copy(backupFile, destFile, overwrite: true);
                        successCount++;
                    }
                }
                catch (Exception ex)
                {
                    context.Logger.LogWarning(ex, "Failed to restore backup for {File}", destFile);
                    errors.Add($"Failed to restore {destFile}: {ex.Message}");
                }
            }

            // Delete newly copied files (in reverse order)
            for (int i = _metadata.CopiedFiles.Count - 1; i >= 0; i--)
            {
                var destFile = _metadata.CopiedFiles[i];

                try
                {
                    // Only delete if it didn't exist before and has no backup
                    if (!_metadata.WasExisting(destFile) && !_metadata.BackupPaths.ContainsKey(destFile))
                    {
                        if (File.Exists(destFile))
                        {
                            context.Logger.LogDebug("Deleting copied file: {File}", destFile);
                            File.Delete(destFile);
                            successCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    context.Logger.LogWarning(ex, "Failed to delete copied file {File}", destFile);
                    errors.Add($"Failed to delete {destFile}: {ex.Message}");
                }
            }

            // Delete created directories (in reverse order, bottom-up)
            for (int i = _metadata.CreatedDirectories.Count - 1; i >= 0; i--)
            {
                var directory = _metadata.CreatedDirectories[i];

                try
                {
                    if (Directory.Exists(directory))
                    {
                        // Only delete if empty
                        if (!Directory.EnumerateFileSystemEntries(directory).Any())
                        {
                            context.Logger.LogDebug("Deleting created directory: {Directory}", directory);
                            Directory.Delete(directory, recursive: false);
                            successCount++;
                        }
                        else
                        {
                            context.Logger.LogDebug("Skipping non-empty directory: {Directory}", directory);
                        }
                    }
                }
                catch (Exception ex)
                {
                    context.Logger.LogWarning(ex, "Failed to delete directory {Directory}", directory);
                    errors.Add($"Failed to delete directory {directory}: {ex.Message}");
                }
            }

            // Determine result
            if (errors.Count == 0)
            {
                context.Logger.LogInformation("Rollback successful. Restored/deleted {Count} items", successCount);
                return Task.FromResult(InstallationStepResult.SuccessResult(
                    $"Rollback successful - restored/deleted {successCount} items"));
            }
            else
            {
                var message = $"Rollback partially successful: {successCount} items restored/deleted, {errors.Count} errors";
                context.Logger.LogWarning("{Message}", message);
                return Task.FromResult(InstallationStepResult.FailureResult(
                    message,
                    data: new Dictionary<string, object> { ["Errors"] = errors }));
            }
        }
        catch (Exception ex)
        {
            context.Logger.LogError(ex, "Unexpected error during rollback");
            return Task.FromResult(InstallationStepResult.FailureResult(
                $"Rollback failed: {ex.Message}",
                ex));
        }
    }

    /// <summary>
    /// Cleans up temporary resources (backup files) created during execution.
    /// </summary>
    public ValueTask DisposeAsync()
    {
        // Clean up all backup files
        foreach (var backupFile in _metadata.BackupPaths.Values)
        {
            try
            {
                if (File.Exists(backupFile))
                {
                    // If copy failed and backup wasn't restored, restore it now
                    if (!_copySucceeded)
                    {
                        var destFile = _metadata.BackupPaths.FirstOrDefault(x => x.Value == backupFile).Key;
                        if (!string.IsNullOrEmpty(destFile) && _metadata.WasExisting(destFile))
                        {
                            File.Copy(backupFile, destFile, overwrite: true);
                        }
                    }

                    // Delete backup
                    File.Delete(backupFile);
                }
            }
            catch
            {
                // Best-effort cleanup, ignore errors
            }
        }

        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    // ============================================
    // Private Helper Methods
    // ============================================

    /// <summary>
    /// Enumerates all files to copy from the source directory based on filter options.
    /// </summary>
    private IEnumerable<FileInfo> EnumerateFilesToCopy(InstallationContext context)
    {
        var sourceDir = new DirectoryInfo(SourcePath);

        return EnumerateFilesRecursive(sourceDir, context);
    }

    /// <summary>
    /// Enumerates all directories to create in the destination to preserve directory structure.
    /// </summary>
    private IEnumerable<DirectoryInfo> EnumerateDirectoriesToCreate(InstallationContext context)
    {
        if (!Options.Recursive)
            return Enumerable.Empty<DirectoryInfo>();

        var sourceDir = new DirectoryInfo(SourcePath);
        return EnumerateDirectoriesRecursive(sourceDir, context);
    }

    /// <summary>
    /// Recursively enumerates directories in a directory tree, applying filters.
    /// </summary>
    private IEnumerable<DirectoryInfo> EnumerateDirectoriesRecursive(DirectoryInfo directory, InstallationContext context)
    {
        IEnumerable<DirectoryInfo> subdirectories;
        try
        {
            subdirectories = directory.EnumerateDirectories();
        }
        catch (Exception ex)
        {
            context.Logger.LogWarning(ex, "Failed to enumerate subdirectories in: {Directory}", directory.FullName);
            yield break;
        }

        foreach (var subdirectory in subdirectories)
        {
            if (_filter.ShouldIncludeDirectory(subdirectory))
            {
                yield return subdirectory;

                // Recursively enumerate subdirectories
                foreach (var subdir in EnumerateDirectoriesRecursive(subdirectory, context))
                {
                    yield return subdir;
                }
            }
        }
    }

    /// <summary>
    /// Recursively enumerates files in a directory, applying filters.
    /// </summary>
    private IEnumerable<FileInfo> EnumerateFilesRecursive(DirectoryInfo directory, InstallationContext context)
    {
        // Enumerate files in current directory
        IEnumerable<FileInfo> files;
        try
        {
            files = directory.EnumerateFiles();
        }
        catch (Exception ex)
        {
            context.Logger.LogWarning(ex, "Failed to enumerate files in directory: {Directory}", directory.FullName);
            yield break;
        }

        foreach (var file in files)
        {
            if (_filter.ShouldIncludeFile(file))
            {
                yield return file;
            }
        }

        // Recursively enumerate subdirectories if recursive option is enabled
        if (Options.Recursive)
        {
            IEnumerable<DirectoryInfo> subdirectories;
            try
            {
                subdirectories = directory.EnumerateDirectories();
            }
            catch (Exception ex)
            {
                context.Logger.LogWarning(ex, "Failed to enumerate subdirectories in: {Directory}", directory.FullName);
                yield break;
            }

            foreach (var subdirectory in subdirectories)
            {
                if (_filter.ShouldIncludeDirectory(subdirectory))
                {
                    foreach (var file in EnumerateFilesRecursive(subdirectory, context))
                    {
                        yield return file;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Estimates the total size of files to be copied (for disk space validation).
    /// </summary>
    private long EstimateDirectorySize(InstallationContext context)
    {
        try
        {
            return EnumerateFilesToCopy(context)
                .Take(1000) // Limit to first 1000 files for performance
                .Sum(f => f.Length);
        }
        catch
        {
            return 0; // Best-effort estimation
        }
    }

    /// <summary>
    /// Copies a single file from source to destination, handling all options.
    /// </summary>
    private void CopyFile(FileInfo sourceFile, string destFilePath, InstallationContext context)
    {
        // Create destination directory if needed
        var destDir = Path.GetDirectoryName(destFilePath);
        if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
        {
            Directory.CreateDirectory(destDir);
            _metadata.RecordCreatedDirectory(destDir);
        }

        // Check if destination exists
        var destExists = File.Exists(destFilePath);

        // Create backup if overwriting existing file
        if (destExists && Options.Overwrite && Options.CreateBackup)
        {
            var backupPath = $"{destFilePath}.backup_{Guid.NewGuid()}";
            context.Logger.LogDebug("Creating backup: {Backup}", backupPath);
            File.Copy(destFilePath, backupPath, overwrite: false);
            _metadata.RecordBackup(destFilePath, backupPath);
        }

        // Copy the file
        context.Logger.LogTrace("Copying file: {Source} -> {Destination}", sourceFile.FullName, destFilePath);
        File.Copy(sourceFile.FullName, destFilePath, overwrite: Options.Overwrite);

        // Record bytes copied
        _metadata.RecordBytesCopied(sourceFile.Length);

        // Preserve attributes
        if (Options.PreserveAttributes)
        {
            try
            {
                File.SetAttributes(destFilePath, sourceFile.Attributes);
            }
            catch (Exception ex)
            {
                context.Logger.LogWarning(ex, "Failed to preserve attributes for {File}", destFilePath);
            }
        }

        // Preserve timestamps
        if (Options.PreserveTimestamps)
        {
            try
            {
                File.SetCreationTime(destFilePath, sourceFile.CreationTime);
                File.SetLastWriteTime(destFilePath, sourceFile.LastWriteTime);
            }
            catch (Exception ex)
            {
                context.Logger.LogWarning(ex, "Failed to preserve timestamps for {File}", destFilePath);
            }
        }

        // Record in metadata
        _metadata.RecordCopiedFile(destFilePath, destExists);
    }
}
