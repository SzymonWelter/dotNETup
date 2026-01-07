using DotNetUp.Core.Execution;
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
    private readonly List<ConfiguredStep> _steps = new();
    private readonly Dictionary<string, object> _properties = new();
    private readonly InstallationOptions _options = new();
    private ILogger? _logger;
    private IProgress<InstallationProgress>? _progress;
    private CancellationToken _cancellationToken = default;
    private string? _installationPath;

    /// <summary>
    /// Adds a step to the installation with default options.
    /// Steps are executed in the order they are added.
    /// </summary>
    /// <param name="step">The installation step to add</param>
    /// <returns>This builder for fluent chaining</returns>
    public InstallationBuilder WithStep(IInstallationStep step)
    {
        if (step == null)
            throw new ArgumentNullException(nameof(step));

        _steps.Add(new ConfiguredStep(step));
        return this;
    }

    /// <summary>
    /// Adds a step to the installation with custom options.
    /// Steps are executed in the order they are added.
    /// </summary>
    /// <param name="step">The installation step to add</param>
    /// <param name="configure">Action to configure step-specific options</param>
    /// <returns>This builder for fluent chaining</returns>
    public InstallationBuilder WithStep(IInstallationStep step, Action<InstallationStepOptions> configure)
    {
        if (step == null)
            throw new ArgumentNullException(nameof(step));
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        var options = new InstallationStepOptions();
        configure(options);
        _steps.Add(new ConfiguredStep(step, options));
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
    /// Configures installation options using a configuration action.
    /// </summary>
    /// <param name="configure">Action to configure options</param>
    /// <returns>This builder for fluent chaining</returns>
    public InstallationBuilder WithOptions(Action<InstallationOptions> configure)
    {
        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        configure(_options);
        return this;
    }

    /// <summary>
    /// Sets the base installation path.
    /// </summary>
    /// <param name="path">Installation directory path</param>
    /// <returns>This builder for fluent chaining</returns>
    public InstallationBuilder WithInstallationPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Installation path cannot be null or empty", nameof(path));

        _installationPath = path;
        return this;
    }

    /// <summary>
    /// Builds the installation configuration.
    /// Returns an installation instance ready to be executed.
    /// </summary>
    /// <returns>An installation instance ready for execution</returns>
    public IInstallation Build()
    {
        if (_steps.Count == 0)
            throw new InvalidOperationException("At least one installation step is required");

        if (_logger == null)
            throw new InvalidOperationException("Logger is required. Use WithLogger() to set it.");

        var context = new InstallationContext(_logger, _progress, _cancellationToken)
        {
            InstallationPath = _installationPath
        };

        // Copy properties to context
        foreach (var kvp in _properties)
        {
            context.Properties[kvp.Key] = kvp.Value;
        }

        return new Installation(_steps.AsReadOnly(), context, _options);
    }
}
