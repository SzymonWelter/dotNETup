namespace DotNetUp.Core.Models;

/// <summary>
/// Configuration options for installation behavior.
/// Controls how the installation executor handles various scenarios.
/// </summary>
public class InstallationOptions
{
    /// <summary>
    /// Automatically rollback on error. Default: true.
    /// When enabled, if any step fails, all previously executed steps will be rolled back.
    /// </summary>
    public bool RollbackOnFailure { get; set; } = true;

    /// <summary>
    /// Validate all steps before executing any. Default: true.
    /// When enabled, all steps are validated upfront before execution begins.
    /// </summary>
    public bool ValidateBeforeInstall { get; set; } = true;

    /// <summary>
    /// Create backup before making changes. Default: true.
    /// When enabled, steps that support backup will create backups before modifying files.
    /// </summary>
    public bool CreateBackup { get; set; } = true;

    /// <summary>
    /// Overall installation timeout. Default: 30 minutes.
    /// The installation will be cancelled if it exceeds this duration.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Require administrator privileges. Default: false.
    /// When enabled, the installation will verify it's running with admin rights before starting.
    /// </summary>
    public bool RequireAdministrator { get; set; } = false;
}
