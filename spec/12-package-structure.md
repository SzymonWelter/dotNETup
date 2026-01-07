# Package Structure

The library is organized into multiple NuGet packages for modularity and flexibility:

## Core Package

**DotNetUp.Core**
- Core abstractions (interfaces)
- Fluent builder API
- Orchestration engine
- Base classes and utilities
- **Dependencies:** None (only .NET BCL and Microsoft.Extensions.Logging.Abstractions)

## Built-in Step Packages

### DotNetUp.Steps.FileSystem
- File copy, move, delete operations
- Directory creation and management
- File permission management
- Symbolic links and junctions
- **Dependencies:** DotNetUp.Core

### DotNetUp.Steps.Registry
- Registry key creation and deletion
- Registry value management
- Permission management
- Windows-only
- **Dependencies:** DotNetUp.Core

### DotNetUp.Steps.Services
- Windows service installation
- Service configuration
- Service start/stop/restart
- **Dependencies:** DotNetUp.Core

### DotNetUp.Steps.Database
- SQL Server migration steps
- PostgreSQL support
- MongoDB support
- Generic SQL script execution
- **Dependencies:** DotNetUp.Core + database drivers

### DotNetUp.Steps.IIS
- Application pool management
- Website creation and configuration
- Application configuration
- Windows-only
- **Dependencies:** DotNetUp.Core + Microsoft.Web.Administration

### DotNetUp.Steps.Network
- HTTP download operations
- Archive extraction (ZIP, TAR, 7Z)
- FTP operations
- Network drive mapping
- **Dependencies:** DotNetUp.Core

## UI Packages (Optional)

### DotNetUp.UI.Console
- RazorConsole integration
- Interactive console installer
- Progress visualization
- **Dependencies:** DotNetUp.Core + RazorConsole

### DotNetUp.UI.WPF
- WPF-based installer UI
- Modern, customizable interface
- **Dependencies:** DotNetUp.Core + WPF

## Testing Package

### DotNetUp.Testing
- Test helpers and utilities
- Mock implementations of steps
- In-memory file system for testing
- Test data builders
- **Dependencies:** DotNetUp.Core

## Standalone Binary

### DotNetUp.Installer (NuGet Global Tool)
- Standalone executable
- All built-in steps included
- Configuration file support
- **Dependencies:** All DotNetUp.Steps.* packages

---

## Versioning Strategy

### Semantic Versioning (SemVer)
- Major version: Breaking changes
- Minor version: New features, backward compatible
- Patch version: Bug fixes

### Package Coordination
- Core and Step packages versioned together
- UI packages can version independently
- Clear compatibility matrix in documentation
