# Package Structure

The library is organized into multiple NuGet packages for modularity and flexibility:

## Core Package

**YourLibrary.Core**
- Core abstractions (interfaces)
- Fluent builder API
- Orchestration engine
- Base classes and utilities
- **Dependencies:** None (only .NET BCL and Microsoft.Extensions.Logging.Abstractions)

## Built-in Step Packages

### YourLibrary.Steps.FileSystem
- File copy, move, delete operations
- Directory creation and management
- File permission management
- Symbolic links and junctions
- **Dependencies:** YourLibrary.Core

### YourLibrary.Steps.Registry
- Registry key creation and deletion
- Registry value management
- Permission management
- Windows-only
- **Dependencies:** YourLibrary.Core

### YourLibrary.Steps.Services
- Windows service installation
- Service configuration
- Service start/stop/restart
- **Dependencies:** YourLibrary.Core

### YourLibrary.Steps.Database
- SQL Server migration steps
- PostgreSQL support
- MongoDB support
- Generic SQL script execution
- **Dependencies:** YourLibrary.Core + database drivers

### YourLibrary.Steps.IIS
- Application pool management
- Website creation and configuration
- Application configuration
- Windows-only
- **Dependencies:** YourLibrary.Core + Microsoft.Web.Administration

### YourLibrary.Steps.Network
- HTTP download operations
- Archive extraction (ZIP, TAR, 7Z)
- FTP operations
- Network drive mapping
- **Dependencies:** YourLibrary.Core

## UI Packages (Optional)

### YourLibrary.UI.Console
- RazorConsole integration
- Interactive console installer
- Progress visualization
- **Dependencies:** YourLibrary.Core + RazorConsole

### YourLibrary.UI.WPF
- WPF-based installer UI
- Modern, customizable interface
- **Dependencies:** YourLibrary.Core + WPF

## Testing Package

### YourLibrary.Testing
- Test helpers and utilities
- Mock implementations of steps
- In-memory file system for testing
- Test data builders
- **Dependencies:** YourLibrary.Core

## Standalone Binary

### YourLibrary.Installer (NuGet Global Tool)
- Standalone executable
- All built-in steps included
- Configuration file support
- **Dependencies:** All YourLibrary.Steps.* packages

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
