# Core Components

## 1. IInstallationStep

The fundamental contract that all installation operations must implement.

**Responsibilities:**
- Define a single unit of installation work
- Support validation before execution
- Execute the installation operation
- Support rollback if needed

**Key Methods:**
- `ExecuteAsync()` - Performs the installation operation
- `RollbackAsync()` - Undoes the operation if installation fails
- `ValidateAsync()` - Checks if the operation can be performed

**Properties:**
- `Name` - Human-readable name of the step
- `Description` - Detailed description of what the step does

**Design Goals:**
- Simple to implement
- Testable in isolation
- Composable into complex workflows
- Self-contained (no hidden dependencies)

---

## 2. InstallationContext

Shared context passed to all steps during execution.

**Responsibilities:**
- Provide shared state between steps
- Provide access to services (logging, progress reporting)
- Carry configuration and properties
- Support cancellation

**Key Properties:**
- `Properties` - Dictionary for sharing data between steps
- `Logger` - ILogger instance for logging
- `Progress` - IProgress<T> for progress reporting
- `CancellationToken` - Support for cancellation
- `InstallationPath` - Base installation directory
- `IsUninstall` - Flag indicating uninstall mode

**Usage Patterns:**
- Steps read configuration from Properties
- Steps write output data to Properties for subsequent steps
- Steps log important events using Logger
- Steps report progress using Progress

---

## 3. InstallationResult

Result returned by each step execution.

**Responsibilities:**
- Indicate success or failure
- Provide error information
- Return output data

**Key Properties:**
- `Success` - Boolean indicating if step succeeded
- `Message` - Human-readable message
- `Exception` - Exception object if step failed
- `Data` - Dictionary for output data

**Design Goals:**
- Clear success/failure indication
- Rich error information for troubleshooting
- Support for data flow between steps

---

## 4. InstallationBuilder

Fluent API for configuring and building installations.

**Responsibilities:**
- Provide intuitive API for defining installations
- Collect steps, properties, and options
- Build the Installation executor

**Key Methods:**
- `WithStep(IInstallationStep)` - Add a step
- `WithProperty(string, object)` - Add configuration property
- `WithLogger(ILogger)` - Configure logging
- `WithProgress(IProgress<T>)` - Configure progress reporting
- `WithOptions(Action<InstallationOptions>)` - Configure options
- `Build()` - Create the Installation executor

**Fluent API Benefits:**
- Readable, self-documenting code
- Compile-time safety
- IntelliSense support
- Method chaining

---

## 5. Installation

The main executor that orchestrates the installation workflow.

**Responsibilities:**
- Execute steps in order
- Handle errors and rollback
- Report progress
- Validate prerequisites
- Manage state

**Key Methods:**
- `InstallAsync()` - Execute the installation
- `UninstallAsync()` - Execute uninstallation (reverse order)
- `RepairAsync()` - Re-execute specific steps

**Execution Flow:**
1. Validation Phase - Validate all steps
2. Execution Phase - Execute steps sequentially
3. Success - Installation complete
4. Failure - Trigger rollback of completed steps

**Error Handling:**
- Automatic rollback on failure (if enabled)
- Rollback executes in reverse order
- Best-effort rollback (continues even if rollback steps fail)
- Comprehensive error logging

---

## 6. InstallationOptions

Configuration options for installation behavior.

**Key Options:**
- `RollbackOnFailure` - Automatically rollback on error (default: true)
- `ValidateBeforeInstall` - Validate before executing (default: true)
- `CreateBackup` - Create backup before changes (default: true)
- `Timeout` - Overall installation timeout (default: 30 minutes)
- `ContinueOnNonCriticalError` - Continue if non-critical step fails
- `RequireAdministrator` - Require admin privileges

---

## 7. InstallationSummary

Summary of installation execution.

**Key Properties:**
- `Success` - Overall success indicator
- `Message` - Summary message
- `Exception` - Exception if failed
- `StepResults` - Dictionary of results for each step
- `Duration` - Total time taken
- `CompletedSteps` - Number of completed steps
- `FailedStep` - Name of step that failed (if any)

**Purpose:**
- Provide complete picture of installation outcome
- Support post-installation analysis
- Enable detailed logging and reporting
