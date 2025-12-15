using DotNetUp.Core.Models;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotNetUp.Tests.Fixtures;

/// <summary>
/// Factory for creating test installation contexts with sensible defaults.
/// </summary>
public static class TestInstallationContext
{
    /// <summary>
    /// Creates a test installation context with optional customization.
    /// </summary>
    public static InstallationContext Create(
        ILogger? logger = null,
        IProgress<InstallationProgress>? progress = null,
        Dictionary<string, object>? properties = null,
        CancellationToken cancellationToken = default)
    {
        var testLogger = logger ?? Substitute.For<ILogger>();
        var context = new InstallationContext(testLogger, progress, cancellationToken);

        if (properties != null)
        {
            foreach (var kvp in properties)
                context.Properties[kvp.Key] = kvp.Value;
        }

        return context;
    }
}
