using DotNetUp.Core.Interfaces;

namespace DotNetUp.Core.Models;

/// <summary>
/// Wraps an installation step with its configuration options.
/// Used internally to pair steps with their per-step settings.
/// </summary>
internal class ConfiguredStep
{
    /// <summary>
    /// The installation step implementation.
    /// </summary>
    public IInstallationStep Step { get; }

    /// <summary>
    /// Configuration options for this step.
    /// </summary>
    public InstallationStepOptions Options { get; }

    /// <summary>
    /// Gets the effective name for this step (options override or step's built-in name).
    /// </summary>
    public string Name => Options.Name ?? Step.Name;

    /// <summary>
    /// Gets the effective description for this step (options override or step's built-in description).
    /// </summary>
    public string Description => Options.Description ?? Step.Description;

    /// <summary>
    /// Creates a new configured step.
    /// </summary>
    /// <param name="step">The step implementation</param>
    /// <param name="options">The step options (uses defaults if null)</param>
    public ConfiguredStep(IInstallationStep step, InstallationStepOptions? options = null)
    {
        Step = step ?? throw new ArgumentNullException(nameof(step));
        Options = options ?? new InstallationStepOptions();
    }
}
