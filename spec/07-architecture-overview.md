# Architecture Overview

The library follows a layered architecture with clear separation of concerns:

```
┌──────────────────────────────────────────────────────────────┐
│                     Consumer Layer                          │
│                                                              │
│  Applications that use the library:                         │
│  - Console Applications                                     │
│  - WPF/Windows Forms Applications                           │
│  - Command-Line Tools                                       │
│  - CI/CD Scripts                                            │
│  - PowerShell Modules                                       │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       │ Uses fluent API
                       │
┌──────────────────────▼──────────────────────────────────────┐
│                   Fluent API Layer                          │
│                                                              │
│  - InstallationBuilder (main entry point)                   │
│  - Fluent configuration methods (WithStep, WithProperty)    │
│  - Presets & Templates (common scenarios)                   │
│  - Configuration-based builder                              │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       │ Builds and configures
                       │
┌──────────────────────▼──────────────────────────────────────┐
│                Core Orchestration Layer                     │
│                                                              │
│  - Installation (main executor)                             │
│  - InstallationContext (shared state & services)            │
│  - Step lifecycle management (execute, validate, rollback)  │
│  - Rollback coordinator (transaction semantics)             │
│  - Validation engine (prerequisite checks)                  │
│  - Progress tracking and reporting                          │
│  - Error handling and recovery                              │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       │ Executes
                       │
┌──────────────────────▼──────────────────────────────────────┐
│                   Plugin/Step Layer                         │
│                                                              │
│  Core Interface:                                            │
│  - IInstallationStep (contract for all steps)              │
│                                                              │
│  Built-in Steps:                                            │
│  - File Operations (copy, move, delete, permissions)       │
│  - Registry Operations (keys, values, permissions)         │
│  - Service Management (Windows services, IIS)              │
│  - Database Operations (migrations, scripts)               │
│  - Download Operations (HTTP downloads)                     │
│  - Archive Operations (extract ZIP, TAR, 7Z)               │
│                                                              │
│  Custom Steps:                                              │
│  - User-defined steps                                       │
│  - Third-party plugins                                      │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       │ Uses
                       │
┌──────────────────────▼──────────────────────────────────────┐
│                Infrastructure Layer                         │
│                                                              │
│  Platform-specific implementations:                         │
│  - File system operations                                   │
│  - Registry operations (Windows)                            │
│  - Process management                                       │
│  - Database connections                                     │
│  - HTTP client operations                                   │
│  - Compression/decompression                                │
└──────────────────────────────────────────────────────────────┘

Cross-Cutting Concerns (Available to all layers):
├── Logging (ILogger integration with Microsoft.Extensions.Logging)
├── Progress Reporting (IProgress<T> integration)
├── Cancellation (CancellationToken support)
├── Error Handling (Exceptions, Result types)
└── Dependency Injection (optional IServiceProvider integration)
```

## Architectural Principles

### 1. Separation of Concerns
- Each layer has a single, well-defined responsibility
- Layers communicate through clear interfaces
- No layer bypasses another

### 2. Dependency Inversion
- Core abstractions depend on interfaces, not implementations
- Plugins depend on core interfaces
- Easy to mock and test

### 3. Open/Closed Principle
- Open for extension (plugins, custom steps)
- Closed for modification (core logic is stable)

### 4. Single Responsibility
- Each step does one thing well
- Steps are composable into complex workflows

### 5. Interface Segregation
- Small, focused interfaces
- Consumers only depend on what they need
