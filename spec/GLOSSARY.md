# Glossary & References

## Glossary

### Installation Step
A single, atomic operation in an installation workflow (e.g., copy file, create registry key)

### Rollback
The process of undoing completed installation steps when an error occurs

### Fluent API
A programming interface that uses method chaining to create readable, self-documenting code

### Plugin
An extension that adds functionality to the core library

### Silent Installation
Installation that runs without user interaction

### Prerequisite
A requirement that must be met before installation can proceed

### Composite Step
A step that internally executes multiple other steps

### Decorator
A wrapper that adds functionality to an existing step

### Idempotency
The property of an operation that can be applied multiple times with the same result as applying it once

### Best-Effort Rollback
A rollback strategy that attempts to undo changes but continues even if some rollback steps fail

### Dry-Run
A simulation of an installation process without making actual changes to the system

### InstallationContext
The shared context object passed to all installation steps, containing configuration, logging, progress reporting, and cancellation support

### InstallationResult
The return value from a step execution, containing success indicator, message, optional exception, and output data

### Transaction-like Semantics
The property of an installation that either completes fully or rolls back completely, leaving the system in a consistent state

---

## References

### Microsoft Documentation
- [Microsoft.Extensions.Logging](https://docs.microsoft.com/en-us/dotnet/api/microsoft.extensions.logging)
- [.NET Dependency Injection patterns](https://docs.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- [IProgress<T> Interface](https://docs.microsoft.com/en-us/dotnet/api/system.iprogress-1)
- [CancellationToken Structure](https://docs.microsoft.com/en-us/dotnet/api/system.threading.cancellationtoken)

### Industry Standards
- [Semantic Versioning (SemVer) specification](https://semver.org/)
- [Enterprise Integration Patterns](https://www.enterpriseintegrationpatterns.com/)
- [SOLID Principles](https://en.wikipedia.org/wiki/SOLID)

### Related Tools & Standards
- [WiX Toolset](https://wixtoolset.org/) - Industry-standard installer creation tool
- [PowerShell DSC](https://docs.microsoft.com/en-us/powershell/dsc/overview) - Declarative State Configuration
- [Squirrel.Windows](https://github.com/Squirrel/Squirrel.Windows) - Modern app deployment
- [OpenStandards.Net Conventions](https://docs.microsoft.com/en-us/dotnet/fundamentals/coding-style-rules)

---

## Related Documentation

- [CLAUDE.md](../CLAUDE.md) - Main project documentation and development workflow
- [Project Architecture](07-architecture-overview.md) - Detailed architecture overview
- [Core Components](08-core-components.md) - Component specifications

---

**Document Version:** 1.0
**Last Updated:** December 2025
