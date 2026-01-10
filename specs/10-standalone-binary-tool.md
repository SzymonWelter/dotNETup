# Standalone Binary Tool

## Overview

A self-contained executable that reads installation configuration from files (JSON, XML, YAML) and executes installations without requiring code.

**Target Audience:**
- System administrators without coding experience
- Software vendors providing customizable installers
- Quick prototyping and testing scenarios

## Features

### 1. Configuration-Based
- Define entire installation in appsettings.json
- Support for JSON, XML, YAML formats
- JSON Schema for validation and IntelliSense
- Variable substitution from multiple sources

### 2. Multiple Operation Modes
- Interactive mode (RazorConsole UI)
- Silent mode (no user interaction)
- Minimal mode (progress only)
- Validate mode (check config without installing)

### 3. Command-Line Interface
- Override properties from command line
- Specify custom config file
- Generate sample configurations
- List available step types
- Dry-run mode (simulate)

### 4. Built-in Step Library
- All standard steps included
- No plugins required for common scenarios
- Extensible via plugin DLLs

---

## Configuration Structure

**Top-Level Sections:**
- `installation` - Main configuration
  - `name` - Application name
  - `version` - Version number
  - `description` - Description
  - `installPath` - Default installation path
  - `options` - Installation options
  - `ui` - UI configuration
  - `properties` - Custom properties
  - `prerequisites` - Prerequisite checks
  - `steps` - Installation steps
  - `postInstall` - Post-installation actions
  - `uninstall` - Uninstallation configuration

---

## Variable Substitution

Support for dynamic values from multiple sources:

**Sources:**
- Environment variables: `${env:VAR_NAME}`
- Command-line arguments: `${arg:ArgName}`
- Interactive prompts: `${prompt:PropertyName}`
- File contents: `${file:path/to/file.txt}`
- Current timestamp: `${now:yyyy-MM-dd}`
- Machine information: `${machine:name}`
- Current user: `${user:name}`
- Installation properties: `${installPath}`

---

## Template System

Built-in templates for common scenarios:

**Available Templates:**
- Web Application (IIS, app pool, website)
- Windows Service (service installation)
- Database Only (schema creation)
- Desktop Application (files, shortcuts)
- Multi-Component (combination)

**Usage:**
```
Generate template:
installer.exe --template web-application > appsettings.json

Customize the generated appsettings.json
Run installation:
installer.exe
```

---

## Distribution Options

### 1. Self-Contained Executable
- Single .exe file (includes .NET runtime)
- 10-50 MB size
- No dependencies
- Easy distribution

### 2. Framework-Dependent
- Smaller executable (<1 MB)
- Requires .NET runtime on target machine
- Faster updates

### 3. NuGet Global Tool
- Install via: `dotnet tool install -g YourLibrary.Installer`
- Run via: `installer --config appsettings.json`
- Automatic updates via NuGet

---

## Benefits of Standalone Tool

### Accessibility
- No coding required
- Non-developers can use it
- Configuration is self-documenting

### Portability
- Single file to distribute
- Works anywhere
- No installation required for installer itself

### Flexibility
- Quick iterations (edit config, not code)
- A/B testing of configurations
- Environment-specific configs

### Standardization
- Same tool for all installations
- Consistent experience
- Centralized updates

### Automation
- Easy to script
- Works in CI/CD
- Batch processing support

---

## Limitations

### Complex Logic
- Limited compared to full programming
- No access to full C# language
- Conditional logic is basic

### Dynamic Behavior
- Cannot make runtime decisions based on complex criteria
- Limited programmatic control flow

### Type Safety
- Config errors found at runtime
- No compile-time checking

### Custom Steps
- Requires plugins for non-standard operations
- Plugin development requires coding

### Recommendation
Use standalone binary for 80% of installations (standard scenarios)
Use programmatic API for complex installations requiring custom logic
