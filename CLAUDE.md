# DotNetUp - Installation Orchestration Library - Quick Reference

**Framework:** .NET 8.0+
**Language:** C#
**Status:** Development Phase

---

## What Problem Does It Solve?

Traditional installation tools (MSI, InstallShield) are:
- Hard to maintain
- Difficult to test
- Not automation-friendly
- Not version-control friendly

**Solution:** A code-first C# library where installations are defined programmatically, tested like normal code, and integrated into CI/CD pipelines.

---

## Business Value

✅ Testable installations (unit tests work)  
✅ Version-controlled (in Git like regular code)  
✅ CI/CD friendly (silent/headless mode)  
✅ Developer experience (fluent API, IntelliSense)  
✅ Extensible (custom steps, plugins)  
✅ Reliable (automatic rollback on failure)  

---

## Architecture - 5 Layers

```
Layer 1: Consumer           (Apps, scripts, pipelines using the library)
          ↓
Layer 2: Fluent API         (InstallationBuilder - fluent interface)
          ↓
Layer 3: Core               (Installation executor, context, orchestration)
          ↓
Layer 4: Steps              (IInstallationStep - operation implementations)
          ↓
Layer 5: Infrastructure     (File system, registry, database, HTTP)
```

---

## The Core Abstraction: IInstallationStep

Every operation (copy file, create registry key, install service, etc.) implements this interface:

```
ValidateAsync()   → Check if the operation can proceed
ExecuteAsync()    → Do the operation
RollbackAsync()   → Undo it if something fails
```

That's it. Every step is independent, testable, and composable.

---

## Main Components

### InstallationBuilder (Fluent API)
Developers configure installations using a fluent interface:
```
.WithStep(step1)
.WithStep(step2)
.WithProperty("key", value)
.Build()
```

### Installation (Executor)
- Validates all steps upfront
- Executes them sequentially
- On failure: rolls back completed steps automatically
- Returns summary of what happened

### InstallationContext (Shared State)
Passed to all steps, contains:
- `Properties` dict for data between steps
- `Logger` for logging
- `Progress` for progress reporting
- `CancellationToken` for cancellation

### InstallationResult (Standard Response)
Every step returns:
- `Success` bool
- `Message` string
- `Exception` if failed
- `Data` dict for output

---

## Key Design Principles

**1. Best-Effort Rollback**
If rollback fails, log it but don't crash. The system is already in error state.

**2. Results Not Exceptions**
Steps return `InstallationResult`, never throw. Makes testing easier and error handling consistent.

**3. Composable Steps**
Each step = one operation. Combine them for complex workflows.

**4. Single Responsibility**
A step does one thing and does it well.

**5. Fully Testable**
All components mockable. Each step independently unit testable.

---

## Built-In Step Types (Later Phases)

- **FileSystem** - Copy, move, delete, permissions, archive extraction
- **Registry** - Windows registry operations
- **Services** - Windows service management
- **Database** - SQL migrations and scripts
- **IIS** - Application pool and website setup
- **Network** - Downloads, FTP, network drive mapping

---

## Phase 1 (Weeks 1-2): MVP Foundation

**Must Build:**
1. Core interfaces (IInstallationStep, InstallationContext, InstallationResult)
2. InstallationBuilder (fluent API)
3. Installation executor with rollback
4. FileSystem step (copy, move, delete operations)
5. Error handling and validation

**Then:** Test everything. Aim for 80%+ coverage.

---

## How to Implement a Step

```
1. Inherit from IInstallationStep
2. Implement ValidateAsync()    → Check prerequisites
3. Implement ExecuteAsync()     → Do the work
4. Implement RollbackAsync()    → Undo the work (best-effort)
5. Each method returns InstallationResult
6. Write unit tests for all three methods
7. Test success case, failure case, rollback case
```

---

## Testing

- Use **xUnit** framework
- Use **NSubstitute** for mocking
- Use **FluentAssertions** for readable assertions
- Create **TestInstallationContext** fixture
- Mock **file system, registry, database** etc.
- Test **success path, failure path, rollback path**
- Test **edge cases** (already exists, missing prerequisites)

**See detailed testing guidelines:** `tests/DotNetUp.Tests/README.md`

---

## Build Commands

**Standard Development Workflow:**

```bash
# Build the solution
dotnet build

# Run all tests
dotnet test

# Run specific test class or method
dotnet test --filter "FullyQualifiedName~CopyFileStep"

# Run with verbose output
dotnet test --verbosity normal
```

**Build Validation Rules:**
- ✅ Always build and test locally after implementation
- ✅ Code must compile with zero errors before committing
- ✅ All tests must pass before moving to next feature
- ✅ Fix any failing tests immediately

---

## Key Decisions (Locked In)

✓ Rollback is best-effort (continue even if rollback fails)  
✓ Steps return InstallationResult (no exceptions from steps)  
✓ 5-layer architecture (separation of concerns)  
✓ Fluent API via InstallationBuilder  
✓ IInstallationStep is the fundamental contract  

---

## Folder Structure

```
src/
  DotNetUp.Core/
    ├── Interfaces/      (IInstallationStep, etc.)
    ├── Models/          (InstallationContext, InstallationResult)
    ├── Builders/        (InstallationBuilder - fluent API)
    ├── Execution/       (Installation executor, orchestration)
    ├── Steps/           (FileSystem, Registry, etc.)
    └── Utilities/       (Helpers)

tests/
  DotNetUp.Tests/
    ├── Interfaces/
    ├── Models/
    ├── Builders/
    ├── Execution/
    ├── Steps/
    └── Fixtures/        (Test helpers)
```

---

## For Claude Code Sessions

**Common Patterns:**
1. "Create this interface with these methods..."
2. "Write comprehensive tests for this class..."
3. "Implement this class to pass these tests..."
4. "Review this for [concern]..."

**What works best:**
- Ask for one file at a time
- Provide interface first, then implementation
- Test-first approach
- Build after each component

---

**Last Updated:** December 2025  
**When to Update:** After major decisions or learning  
**Purpose:** Quick reference during development - keep this open