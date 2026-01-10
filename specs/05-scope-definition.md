# Scope Definition

## What the Library IS ✓

### Installation Orchestration Framework
- Coordinates multiple installation steps in sequence
- Manages shared state and context between steps
- Provides transaction-like semantics with rollback
- Handles dependencies and ordering

### Workflow Engine for Installation Tasks
- File operations (copy, move, delete, permissions)
- Registry modifications (create, update, delete keys/values)
- Database migrations (schema creation, data seeding)
- Service installation (Windows services, IIS configuration)
- Configuration management (transformations, merging)

### Developer-Friendly API
- Fluent builder pattern for defining installations
- Strongly-typed configuration
- Rich progress reporting
- Integration with ILogger and IProgress<T>

### Extensibility Platform
- Plugin system for custom installation steps
- Hooks for pre/post installation actions
- Template/preset system for common scenarios
- Third-party integration points

## What the Library IS NOT ✗

### Not a Package Manager
- Does not handle dependency resolution
- Not a replacement for NuGet, Chocolatey, or winget
- Does not manage package repositories

### Not an MSI/Installer File Generator
- Does not create .msi or .exe installer files
- Focused on orchestration, not packaging format
- Can be embedded in installers, but doesn't create them

### Not a Deployment Tool
- Not for deploying to remote servers (Octopus Deploy, Ansible)
- Not for container orchestration (Docker/Kubernetes)
- Local machine installations only

### Not an Update/Patching System
- Not handling version checks or auto-updates
- Not a replacement for Squirrel or similar tools
- Though it could be used as part of an update process

### Not a Configuration Management System
- Not a replacement for Ansible, Chef, Puppet
- Focused on initial installation, not ongoing state management
- Not for infrastructure as code scenarios
