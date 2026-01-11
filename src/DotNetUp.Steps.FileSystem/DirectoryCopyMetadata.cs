namespace DotNetUp.Steps.FileSystem;

/// <summary>
/// Tracks metadata and state for a directory copy operation.
/// Used for rollback, progress reporting, and diagnostic information.
/// </summary>
public class DirectoryCopyMetadata
{
    /// <summary>
    /// Gets the list of files that were successfully copied.
    /// Each entry is the destination file path.
    /// </summary>
    public List<string> CopiedFiles { get; } = new();

    /// <summary>
    /// Gets the list of directories that were created.
    /// Each entry is the destination directory path.
    /// Tracked in creation order for proper rollback (delete in reverse order).
    /// </summary>
    public List<string> CreatedDirectories { get; } = new();

    /// <summary>
    /// Gets the mapping of destination files to their backup paths.
    /// Key: destination file path, Value: backup file path.
    /// Used for rollback to restore original files.
    /// </summary>
    public Dictionary<string, string> BackupPaths { get; } = new();

    /// <summary>
    /// Gets the list of files that existed before the copy operation.
    /// Used to determine which files to delete during rollback.
    /// </summary>
    public HashSet<string> ExistingFiles { get; } = new();

    /// <summary>
    /// Gets the total number of files processed.
    /// </summary>
    public int TotalFilesProcessed => CopiedFiles.Count;

    /// <summary>
    /// Gets the total number of directories created.
    /// </summary>
    public int TotalDirectoriesCreated => CreatedDirectories.Count;

    /// <summary>
    /// Gets the total number of backups created.
    /// </summary>
    public int TotalBackupsCreated => BackupPaths.Count;

    /// <summary>
    /// Gets the total number of bytes copied across all files.
    /// </summary>
    public long TotalBytesCopied { get; private set; }

    /// <summary>
    /// Gets the duration of the copy operation.
    /// </summary>
    public TimeSpan? CopyDuration { get; private set; }

    /// <summary>
    /// Records that a file was successfully copied.
    /// </summary>
    /// <param name="destinationPath">Destination file path</param>
    /// <param name="wasExisting">Whether the file existed before the copy</param>
    public void RecordCopiedFile(string destinationPath, bool wasExisting)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("Destination path cannot be null or whitespace", nameof(destinationPath));

        CopiedFiles.Add(destinationPath);

        if (wasExisting)
        {
            ExistingFiles.Add(destinationPath);
        }
    }

    /// <summary>
    /// Records that a directory was created.
    /// </summary>
    /// <param name="directoryPath">Directory path</param>
    public void RecordCreatedDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("Directory path cannot be null or whitespace", nameof(directoryPath));

        CreatedDirectories.Add(directoryPath);
    }

    /// <summary>
    /// Records that a backup was created for a file.
    /// </summary>
    /// <param name="destinationPath">Destination file path</param>
    /// <param name="backupPath">Backup file path</param>
    public void RecordBackup(string destinationPath, string backupPath)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("Destination path cannot be null or whitespace", nameof(destinationPath));

        if (string.IsNullOrWhiteSpace(backupPath))
            throw new ArgumentException("Backup path cannot be null or whitespace", nameof(backupPath));

        BackupPaths[destinationPath] = backupPath;
    }

    /// <summary>
    /// Determines whether a file existed before the copy operation.
    /// </summary>
    /// <param name="destinationPath">File path to check</param>
    /// <returns>True if the file existed before; otherwise, false.</returns>
    public bool WasExisting(string destinationPath)
    {
        return ExistingFiles.Contains(destinationPath);
    }

    /// <summary>
    /// Records the number of bytes copied for a file.
    /// </summary>
    /// <param name="bytes">Number of bytes copied</param>
    public void RecordBytesCopied(long bytes)
    {
        TotalBytesCopied += bytes;
    }

    /// <summary>
    /// Sets the copy operation duration.
    /// </summary>
    /// <param name="duration">Duration of the copy operation</param>
    public void SetCopyDuration(TimeSpan duration)
    {
        CopyDuration = duration;
    }

    /// <summary>
    /// Returns a string representation of the metadata for logging.
    /// </summary>
    public override string ToString()
    {
        return $"Files Copied: {TotalFilesProcessed}, " +
               $"Directories Created: {TotalDirectoriesCreated}, " +
               $"Backups Created: {TotalBackupsCreated}";
    }
}
