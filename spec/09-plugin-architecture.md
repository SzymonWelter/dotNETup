# Plugin Architecture

## Plugin Types

### 1. Installation Steps (IInstallationStep)

The primary plugin type - atomic operations that can be composed into workflows.

**Characteristics:**
- Implement IInstallationStep interface (extends IAsyncDisposable)
- Self-contained and stateless
- Support execution, validation, rollback, and disposal
- Can be unit tested independently
- Automatically cleaned up after execution

**Core Methods:**
- `ValidateAsync()` - Check prerequisites
- `ExecuteAsync()` - Perform the operation
- `RollbackAsync()` - Undo changes (best-effort)
- `DisposeAsync()` - Clean up temporary resources (always called)

**Examples:**
- File copy, move, delete operations
- Archive extraction
- Registry key creation
- Service installation
- Database migration
- Custom business logic

**Resource Management:**
Steps that create temporary resources (backups, temp files, locks) must implement `DisposeAsync()` to ensure cleanup. The framework guarantees disposal is called regardless of success/failure or ContinueOnError settings.

**Registration:**
```
Option 1: Explicit registration
Option 2: Assembly scanning (convention-based)
Option 3: Directory-based plugins
Option 4: NuGet package plugins
```

---

### 2. Validators (IInstallationValidator)

Plugins that check prerequisites and validate environment.

**Responsibilities:**
- Check OS version
- Verify disk space
- Validate software dependencies
- Check network connectivity
- Verify permissions

**Execution:**
- Run before installation begins
- All validators must pass
- Clear error messages on failure

---

### 3. Hooks (IInstallationHook)

Event handlers for installation lifecycle events.

**Hook Points:**
- PreInstallation - Before any steps execute
- PostInstallation - After all steps complete
- OnStepStart - Before each step
- OnStepComplete - After each step
- OnError - When error occurs
- OnRollback - During rollback process

**Use Cases:**
- Telemetry collection
- Audit logging
- Custom notifications
- Integration with external systems

---

### 4. Providers (IInstallationProvider)

Supply configuration, credentials, or resources.

**Types:**
- Configuration Providers (JSON, XML, environment variables)
- Credential Providers (secure credential storage)
- Resource Providers (download locations, CDN URLs)

**Benefits:**
- Externalize configuration
- Support multiple environments
- Secure credential management

---

### 5. Reporters (IProgressReporter)

Custom progress reporting implementations.

**Types:**
- Console Reporter (text-based)
- GUI Reporter (Windows Forms, WPF)
- File Reporter (log to file)
- Telemetry Reporter (send to monitoring system)
- Webhook Reporter (HTTP callbacks)

**Responsibilities:**
- Receive progress updates
- Format and display progress
- Support multiple simultaneous reporters

---

## Plugin Discovery Mechanisms

### 1. Explicit Registration (Simplest)
- Developer manually adds plugins to builder
- Full control over what is loaded
- No magic, clear and obvious

### 2. Assembly Scanning (Convention-Based)
- Automatically discover plugins in assemblies
- Convention: classes implementing IInstallationStep
- Optional: use attributes for metadata

### 3. Directory-Based (Most Flexible)
- Load plugins from a directory
- Drop DLLs into /plugins folder
- Supports third-party plugins
- Isolated loading (separate AppDomain or AssemblyLoadContext)

### 4. NuGet Packages
- Distribute plugins as NuGet packages
- Standard dependency management
- Versioning and updates
- Reference in project file

---

## Plugin Metadata

Plugins can declare metadata using attributes:

**Metadata Properties:**
- Name - Display name
- Category - Grouping (Database, FileSystem, Network)
- Version - Plugin version
- Author - Plugin creator
- Description - What the plugin does
- Dependencies - Required other plugins
- Platform - Supported platforms (Windows, Linux, macOS)

---

## Plugin Composition Patterns

### Step Decorators

Wrap existing steps to add functionality:
- Retry logic
- Timing/performance measurement
- Logging enhancement
- Caching
- Conditional execution

### Composite Steps

Group multiple steps into a single logical unit:
- WebApplicationInstall (IIS + files + config)
- DatabaseSetup (create DB + schema + seed data)
- ServiceInstall (install + configure + start)
- ExtractAndConfigure (extract archive + copy files + modify registry)

### Conditional Steps

Execute steps based on conditions:
- Based on properties (InstallType == "Full")
- Based on environment (Production vs Development)
- Based on prerequisites (Only if SQL Server exists)

### Parallel Steps

Execute steps concurrently:
- Multiple downloads
- Multiple file extractions
- Independent installations
- Performance optimization

---

## Built-in Step Packages

### FileSystem Package (DotNetUp.Steps.FileSystem)

**Scope:** File and directory operations, archive handling

**Operations:**
- File copy, move, delete
- Directory copy, move, delete (recursive)
- File permission management
- Symbolic links and junctions
- Archive extraction (ZIP, TAR, 7Z)

**Dependencies:** DotNetUp.Core only

**Platform Support:** Windows, Linux, macOS

### Network Package (DotNetUp.Steps.Network)

**Scope:** Network-based data transfer

**Operations:**
- HTTP/HTTPS download operations
- FTP operations
- Network drive mapping (Windows)

**Dependencies:** DotNetUp.Core only

**Platform Support:** Varies by operation (FTP/mapping Windows-only)

### Registry Package (DotNetUp.Steps.Registry)

**Scope:** Windows registry modifications

**Operations:**
- Registry key creation, update, deletion
- Registry value management
- Permission management

**Dependencies:** DotNetUp.Core

**Platform Support:** Windows only

### Services Package (DotNetUp.Steps.Services)

**Scope:** Windows service management

**Operations:**
- Service installation
- Service configuration
- Service start/stop/restart

**Dependencies:** DotNetUp.Core

**Platform Support:** Windows only

### Database Package (DotNetUp.Steps.Database)

**Scope:** Database operations and migrations

**Operations:**
- SQL Server migration steps
- PostgreSQL support
- MongoDB support
- Generic SQL script execution

**Dependencies:** DotNetUp.Core + database drivers

**Platform Support:** Cross-platform

### IIS Package (DotNetUp.Steps.IIS)

**Scope:** Internet Information Services management

**Operations:**
- Application pool management
- Website creation and configuration
- Application configuration
- Bindings and SSL setup

**Dependencies:** DotNetUp.Core + Microsoft.Web.Administration

**Platform Support:** Windows only
