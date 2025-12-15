using DotNetUp.Core.Interfaces;
using DotNetUp.Core.Models;
using Microsoft.Extensions.Logging;

namespace DotNetUp.Core.Builders;

/// <summary>
/// Fluent API for building an installation configuration.
/// Allows developers to define installation steps using a clean, readable syntax.
/// </summary>
public class InstallationBuilder
{
    private readonly List<IInstallationStep> _steps = new();
    private readonly Dictionary<string, object> _properties = new();
    private ILogger? _logger;
    private IProgress<InstallationProgress>? _progress;
    private CancellationToken _cancellationToken = default;

    /// <summary>
    /// Adds a step to the installation.
    /// Steps are executed in the order they are added.
    /// </summary>
    /// <param name="step">The installation step to add</param>
    /// <returns>This builder for fluent chaining</returns>
    public InstallationBuilder WithStep(IInstallationStep step)
    {
        if (step == null)
            throw new ArgumentNullException(nameof(step));

        _steps.Add(step);
        return this;
    }

    /// <summary>
    /// Adds a property to the installation context.
    /// Properties can be accessed by all steps during execution.
    /// </summary>
    /// <param name="key">Property key</param>
    /// <param name="value">Property value</param>
    /// <returns>This builder for fluent chaining</returns>
    public InstallationBuilder WithProperty(string key, object value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Property key cannot be null or empty", nameof(key));

        _properties[key] = value;
        return this;
    }

    /// <summary>
    /// Sets the logger for the installation.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <returns>This builder for fluent chaining</returns>
    public InstallationBuilder WithLogger(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        return this;
    }

    /// <summary>
    /// Sets the progress reporter for the installation.
    /// </summary>
    /// <param name="progress">Progress reporter</param>
    /// <returns>This builder for fluent chaining</returns>
    public InstallationBuilder WithProgress(IProgress<InstallationProgress> progress)
    {
        _progress = progress;
        return this;
    }

    /// <summary>
    /// Sets the cancellation token for the installation.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>This builder for fluent chaining</returns>
    public InstallationBuilder WithCancellationToken(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        return this;
    }

    /// <summary>
    /// Builds the installation configuration.
    /// Returns the list of steps and the context to use for execution.
    /// </summary>
    /// <returns>A tuple containing the steps and context</returns>
    public (IReadOnlyList<IInstallationStep> Steps, InstallationContext Context) Build()
    {
        if (_steps.Count == 0)
            throw new InvalidOperationException("At least one installation step is required");

        if (_logger == null)
            throw new InvalidOperationException("Logger is required. Use WithLogger() to set it.");

        var context = new InstallationContext(_logger, _progress, _cancellationToken);

        // Copy properties to context
        foreach (var kvp in _properties)
        {
            context.Properties[kvp.Key] = kvp.Value;
        }

        return (_steps.AsReadOnly(), context);
    }
}
