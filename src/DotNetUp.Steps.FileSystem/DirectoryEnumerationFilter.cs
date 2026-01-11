namespace DotNetUp.Steps.FileSystem;

/// <summary>
/// Provides filtering logic for enumerating files and directories during copy operations.
/// Supports include/exclude patterns, hidden file filtering, and wildcard matching.
/// </summary>
public class DirectoryEnumerationFilter
{
    private readonly CopyDirectoryOptions _options;

    /// <summary>
    /// Creates a new filter with the specified options.
    /// </summary>
    /// <param name="options">Copy options containing filter criteria</param>
    public DirectoryEnumerationFilter(CopyDirectoryOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Determines whether a file should be included in the copy operation.
    /// </summary>
    /// <param name="fileInfo">File information to evaluate</param>
    /// <returns>True if the file should be copied; otherwise, false.</returns>
    public bool ShouldIncludeFile(FileInfo fileInfo)
    {
        if (fileInfo == null)
            throw new ArgumentNullException(nameof(fileInfo));

        // Check hidden files
        if (!_options.IncludeHidden && fileInfo.Attributes.HasFlag(FileAttributes.Hidden))
        {
            return false;
        }

        var fileName = fileInfo.Name;

        // Check exclude patterns first (they take precedence)
        if (_options.ExcludePatterns.Any(pattern => MatchesPattern(fileName, pattern)))
        {
            return false;
        }

        // If include patterns are specified, file must match at least one
        if (_options.IncludePatterns.Count > 0)
        {
            return _options.IncludePatterns.Any(pattern => MatchesPattern(fileName, pattern));
        }

        // No include patterns specified, include by default
        return true;
    }

    /// <summary>
    /// Determines whether a directory should be traversed during copy operation.
    /// </summary>
    /// <param name="directoryInfo">Directory information to evaluate</param>
    /// <returns>True if the directory should be traversed; otherwise, false.</returns>
    public bool ShouldIncludeDirectory(DirectoryInfo directoryInfo)
    {
        if (directoryInfo == null)
            throw new ArgumentNullException(nameof(directoryInfo));

        // Check hidden directories
        if (!_options.IncludeHidden && directoryInfo.Attributes.HasFlag(FileAttributes.Hidden))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Matches a file name against a wildcard pattern.
    /// Supports * (zero or more characters) and ? (single character) wildcards.
    /// </summary>
    /// <param name="fileName">File name to match</param>
    /// <param name="pattern">Wildcard pattern</param>
    /// <returns>True if the file name matches the pattern; otherwise, false.</returns>
    private static bool MatchesPattern(string fileName, string pattern)
    {
        if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(pattern))
            return false;

        // Convert wildcard pattern to regex pattern
        // Escape special regex characters except * and ?
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*")
            .Replace("\\?", ".") + "$";

        return System.Text.RegularExpressions.Regex.IsMatch(
            fileName,
            regexPattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
