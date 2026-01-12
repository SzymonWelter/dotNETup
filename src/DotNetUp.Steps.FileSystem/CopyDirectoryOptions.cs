namespace DotNetUp.Steps.FileSystem;

/// <summary>
/// Configuration options for directory copying operations.
/// Provides control over filtering, overwrite behavior, attribute preservation, and backup creation.
/// </summary>
public class CopyDirectoryOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to copy subdirectories recursively.
    /// Default is true.
    /// </summary>
    public bool Recursive { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to overwrite existing files in the destination.
    /// If true and a file exists, a backup is created for rollback.
    /// Default is false.
    /// </summary>
    public bool Overwrite { get; init; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to create backups of overwritten files.
    /// Only applies when Overwrite is true.
    /// Default is false.
    /// </summary>
    public bool CreateBackup { get; init; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to preserve file attributes (ReadOnly, Hidden, etc.).
    /// Default is true.
    /// </summary>
    public bool PreserveAttributes { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to preserve file timestamps (CreationTime, LastWriteTime).
    /// Default is true.
    /// </summary>
    public bool PreserveTimestamps { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to copy hidden files and directories.
    /// Default is false.
    /// </summary>
    public bool IncludeHidden { get; init; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to create the destination directory if it doesn't exist.
    /// If false, the destination directory must already exist.
    /// Default is true.
    /// </summary>
    public bool CreateDestinationIfMissing { get; init; } = true;

    /// <summary>
    /// Gets or sets file patterns to include (e.g., "*.txt", "*.dll").
    /// If empty, all files are included. Patterns use standard wildcard matching.
    /// Default is empty (include all files).
    /// </summary>
    public IReadOnlyList<string> IncludePatterns { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets file patterns to exclude (e.g., "*.tmp", "*.log").
    /// Exclude patterns take precedence over include patterns.
    /// Default is empty (no exclusions).
    /// </summary>
    public IReadOnlyList<string> ExcludePatterns { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the interval at which to report progress (number of files).
    /// Progress is reported every N files copied.
    /// Default is 100.
    /// </summary>
    public int ProgressInterval { get; init; } = 100;

    /// <summary>
    /// Validates the options to ensure they are consistent and valid.
    /// </summary>
    /// <returns>A tuple containing validation success and an error message if validation failed.</returns>
    public (bool IsValid, string? ErrorMessage) Validate()
    {
        if (ProgressInterval <= 0)
        {
            return (false, "ProgressInterval must be greater than 0");
        }

        // Validate include patterns
        foreach (var pattern in IncludePatterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return (false, "Include patterns cannot be null or whitespace");
            }

            if (!IsValidGlobPattern(pattern))
            {
                return (false, $"Invalid include pattern: '{pattern}'. Patterns can only contain alphanumeric characters, dots, wildcards (* and ?), hyphens, and underscores.");
            }
        }

        // Validate exclude patterns
        foreach (var pattern in ExcludePatterns)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                return (false, "Exclude patterns cannot be null or whitespace");
            }

            if (!IsValidGlobPattern(pattern))
            {
                return (false, $"Invalid exclude pattern: '{pattern}'. Patterns can only contain alphanumeric characters, dots, wildcards (* and ?), hyphens, and underscores.");
            }
        }

        // Check for contradictory patterns (same pattern in both include and exclude)
        var contradictoryPatterns = IncludePatterns.Intersect(ExcludePatterns, StringComparer.OrdinalIgnoreCase).ToList();
        if (contradictoryPatterns.Count > 0)
        {
            return (false, $"Pattern(s) appear in both include and exclude lists: {string.Join(", ", contradictoryPatterns)}. Exclude patterns take precedence.");
        }

        return (true, null);
    }

    /// <summary>
    /// Validates that a glob pattern contains only safe characters.
    /// </summary>
    /// <param name="pattern">Pattern to validate</param>
    /// <returns>True if the pattern is valid; otherwise, false.</returns>
    private static bool IsValidGlobPattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return false;

        // Allow alphanumeric, wildcards, dots, hyphens, underscores, and forward/back slashes
        foreach (char c in pattern)
        {
            if (!char.IsLetterOrDigit(c) &&
                c != '*' &&
                c != '?' &&
                c != '.' &&
                c != '-' &&
                c != '_' &&
                c != '/' &&
                c != '\\')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Returns a string representation of the options for logging.
    /// </summary>
    public override string ToString()
    {
        return $"Recursive={Recursive}, Overwrite={Overwrite}, CreateBackup={CreateBackup}, " +
               $"PreserveAttributes={PreserveAttributes}, PreserveTimestamps={PreserveTimestamps}, " +
               $"IncludeHidden={IncludeHidden}, CreateDestinationIfMissing={CreateDestinationIfMissing}, " +
               $"IncludePatterns=[{string.Join(", ", IncludePatterns)}], " +
               $"ExcludePatterns=[{string.Join(", ", ExcludePatterns)}], ProgressInterval={ProgressInterval}";
    }
}
