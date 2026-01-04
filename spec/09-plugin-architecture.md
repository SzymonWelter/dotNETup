# Plugin Architecture

## Plugin Types

### 1. Installation Steps (IInstallationStep)

The primary plugin type - atomic operations that can be composed into workflows.

**Characteristics:**
- Implement IInstallationStep interface
- Self-contained and stateless
- Support execution, validation, and rollback
- Can be unit tested independently

**Examples:**
- File copy operation
- Registry key creation
- Service installation
- Database migration
- Custom business logic

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

### Conditional Steps

Execute steps based on conditions:
- Based on properties (InstallType == "Full")
- Based on environment (Production vs Development)
- Based on prerequisites (Only if SQL Server exists)

### Parallel Steps

Execute steps concurrently:
- Multiple downloads
- Independent installations
- Performance optimization
