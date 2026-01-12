# Implementation Plan: CreateDirectoryStep

## Overview

The `CreateDirectoryStep` implementation will add directory creation and management capabilities to the DotNetUp installation library. This is a foundational operation needed by other steps (like `CopyFileStep`) that require directories to exist before files can be placed in them.

The implementation follows the existing codebase patterns:
- Inherits from `IInstallationStep` interface
- Returns `InstallationStepResult` for all outcomes
- Implements three core methods: `ValidateAsync()`, `ExecuteAsync()`, `RollbackAsync()`
- Includes comprehensive logging and error handling
- Supports best-effort rollback with state tracking
- Handles cross-platform differences (Windows vs Unix-like systems)

## Prerequisites

The following must already exist and function correctly:
- `IInstallationStep` interface (core contract)
- `InstallationContext` (provides logging, properties, cancellation)
- `InstallationStepResult` (standard return type)
- `DotNetUp.Steps.FileSystem` project (where the step will be implemented)
- Test infrastructure with `TestInstallationContext` fixture
- xUnit, NSubstitute, and FluentAssertions testing libraries

## Architecture & Design Decisions

### Directory Path Handling
- Paths can be absolute or relative to `InstallationContext.InstallationPath` if specified
- The step will normalize paths using `Path.GetFullPath()` to handle relative paths
- Path validation includes OS-specific checks (Windows 260-char limit, invalid characters)

### Permission Management
- Platform-specific implementation: Windows ACLs vs Unix chmod
- Permission setting is optional and best-effort
- If permission setting fails, directory creation succeeds but logged as warning
- Backup original permissions before modification for accurate rollback

### State Tracking
- Track whether directory was pre-existing (skip creation if exists and allowed)
- Track count of parent directories created (for rollback cleanup)
- Store original permissions if modified (for rollback restoration)
- Mark completion states to guide rollback logic

### Rollback Strategy
1. **Restore Permissions First** - If permissions were changed, restore originals
2. **Remove Empty Directories** - Only delete created directories if empty
3. **Walk Back Parent Chain** - Remove parent dirs created during execution if empty
4. **Handle In-Use Directories** - Log warnings for locked/in-use directories, continue best-effort

## Phase 1: Core Step Implementation

**Goal**: Create the `CreateDirectoryStep` class with validation, execution, and rollback logic

**Validation**: Step class compiles, implements all required members, basic instantiation works

### Task 1.1: Create CreateDirectoryStep Class Structure

**Description**:
Create the `CreateDirectoryStep` class skeleton with public properties and constructor. This establishes the basic interface and allows parameter configuration.

**Dependencies**:
- IInstallationStep interface exists
- DotNetUp.Steps.FileSystem project exists

**Files**:
- `/home/swelter/Projects/dotNETup/src/DotNetUp.Steps.FileSystem/CreateDirectoryStep.cs` (new)

**Implementation Details**:
- Define public property: `string DirectoryPath { get; }`
- Constructor validates DirectoryPath is not null/empty/whitespace
- Implement `IInstallationStep` members (Name, Description)
- Extract configuration from InstallationContext.Properties in validation/execution
- Use reasonable defaults: CreateParentDirectories=true, AllowIfAlreadyExists=false, SetPermissions=false

**Tests**:
- Constructor with valid path sets property
- Constructor with null path throws ArgumentException
- Constructor with empty/whitespace path throws ArgumentException
- Name property returns "CreateDirectory"
- Description includes the directory path

**Acceptance Criteria**:
- [ ] Class inherits from IInstallationStep
- [ ] Public DirectoryPath property accessible
- [ ] Constructor validates input parameters
- [ ] Name and Description properties implemented
- [ ] Code compiles without errors
- [ ] Constructor tests pass

**Complexity**: Simple

---

### Task 1.2: Implement ValidateAsync - Path Validation

**Description**:
Implement path validation logic in `ValidateAsync()` to check directory paths are valid for the OS, not exceeding limits, and have valid characters.

**Dependencies**:
- Task 1.1: CreateDirectoryStep class structure exists

**Files**:
- `/home/swelter/Projects/dotNETup/src/DotNetUp.Steps.FileSystem/CreateDirectoryStep.cs` (modify)

**Implementation Details**:
- Extract DirectoryPath from constructor or context properties
- Resolve relative paths using InstallationContext.InstallationPath
- Call Path.GetFullPath() for path normalization
- Validate absolute path (not null after normalization)
- Check path length:
  - Windows: 260 chars for standard paths, unlimited with \\?\ prefix
  - Unix: No practical limit, just validate reasonable length
- Validate path characters using Path.GetInvalidPathChars()
- Reject invalid locations (root device paths, network special paths)
- Return clear error messages with specific validation failure reason

**Tests**:
- Valid absolute path passes validation
- Valid relative path (with InstallationPath context) resolves and passes
- Null/empty path fails with appropriate message
- Path exceeding OS limits fails with appropriate message
- Path with invalid characters fails appropriately
- Special invalid paths (device paths on Windows) fail appropriately

**Acceptance Criteria**:
- [ ] Path normalization works for absolute and relative paths
- [ ] OS path length limits enforced correctly
- [ ] Invalid character detection works
- [ ] Special/invalid path detection works
- [ ] Returns InstallationStepResult with clear error messages
- [ ] All path validation tests pass

**Complexity**: Medium

---

### Task 1.3: Implement ValidateAsync - Parent and Existence Checks

**Description**:
Implement logic to check parent directory accessibility and directory existence conditions based on AllowIfAlreadyExists flag.

**Dependencies**:
- Task 1.2: Path validation complete

**Files**:
- `/home/swelter/Projects/dotNETup/src/DotNetUp.Steps.FileSystem/CreateDirectoryStep.cs` (modify)

**Implementation Details**:
- Check if directory already exists
- If exists and AllowIfAlreadyExists=false, return failure
- If exists and AllowIfAlreadyExists=true, return success (skip creation)
- If doesn't exist:
  - Extract CreateParentDirectories flag from context
  - If CreateParentDirectories=true:
    - Get parent directory path
    - Try to access/verify parent exists or can be created
    - Check parent directory is writable
    - Try creating temp file in parent to verify write permission
  - If CreateParentDirectories=false:
    - Verify parent directory already exists
- Handle special cases: parent is actually a file (not directory)

**Tests**:
- Directory exists, AllowIfAlreadyExists=false: fails validation
- Directory exists, AllowIfAlreadyExists=true: passes validation
- Directory doesn't exist, parent exists, writable: passes validation
- Directory doesn't exist, parent doesn't exist, CreateParentDirectories=false: fails
- Directory doesn't exist, parent doesn't exist, CreateParentDirectories=true: passes
- Parent path is a file (not directory): fails validation
- Parent directory not writable: fails validation

**Acceptance Criteria**:
- [ ] Existing directory detection works correctly
- [ ] AllowIfAlreadyExists flag respected
- [ ] Parent directory validation handles all cases
- [ ] Write permission detection via temp file works
- [ ] Temp files from validation are cleaned up
- [ ] All existence and parent validation tests pass

**Complexity**: Medium

---

### Task 1.4: Implement ValidateAsync - Complete and Logging

**Description**:
Add logging throughout ValidateAsync, handle exceptions gracefully, and ensure complete validation flow.

**Dependencies**:
- Task 1.3: Parent and existence checks implemented

**Files**:
- `/home/swelter/Projects/dotNETup/src/DotNetUp.Steps.FileSystem/CreateDirectoryStep.cs` (modify)

**Implementation Details**:
- Add LogDebug at start: "Validating CreateDirectoryStep: {Path}"
- Add LogDebug for each validation stage result
- Add LogDebug at success: "Validation successful"
- Wrap entire ValidateAsync in try-catch
- On exception: LogError with exception, return FailureResult with message
- Ensure temp files created during validation are always cleaned up
- Return appropriate InstallationStepResult for each path (success or failure)

**Tests**:
- Full validation flow with valid inputs logs appropriately
- Validation failures are logged at correct levels (Debug, Error, etc.)
- Exceptions during validation are caught and logged
- Temp files are cleaned up even if validation throws

**Acceptance Criteria**:
- [ ] Logging present at all key decision points
- [ ] Exception handling works without crashes
- [ ] Error messages are specific and actionable
- [ ] Temp files always cleaned up
- [ ] ValidateAsync completes with correct result

**Complexity**: Simple

---

### Task 1.5: Implement ExecuteAsync - Directory Creation

**Description**:
Implement the core directory creation logic in `ExecuteAsync()`, including parent directory creation, permission setting, and state tracking.

**Dependencies**:
- Task 1.1-1.4: ValidateAsync complete

**Files**:
- `/home/swelter/Projects/dotNETup/src/DotNetUp.Steps.FileSystem/CreateDirectoryStep.cs` (modify)

**Implementation Details**:
- At start, log: "Executing CreateDirectoryStep: {DirectoryPath}"
- Track state fields:
  - `_directoryExistedBefore` - was directory pre-existing
  - `_parentDirectoriesCreated` - count of parent dirs created
  - `_originalPermissions` - backup of original permissions (if changed)
  - `_creationSucceeded` - flag for rollback logic
- Extract configuration flags from context properties:
  - DirectoryPath (required)
  - CreateParentDirectories (default: true)
  - AllowIfAlreadyExists (default: false)
  - SetPermissions (default: false)
  - Permissions (default: null)
  - Owner (default: null)
  - Group (default: null)
  - BackupExistingPermissions (default: false)
- Check if directory exists before creation
  - If exists: set _directoryExistedBefore=true, return success (per validation)
  - If not exists: proceed to creation
- Create parent directories recursively if needed:
  - Track count in _parentDirectoriesCreated
  - Log each parent creation
- Create target directory:
  - Use Directory.CreateDirectory()
  - Verify creation successful
  - Set _creationSucceeded=true
- Set permissions if configured:
  - Backup original permissions if BackupExistingPermissions=true
  - Apply SetPermissions logic (platform-specific)
  - Continue if permission setting fails (log warning, don't fail execution)
- Store in context.Properties:
  - "CreatedDirectory" -> full resolved path
  - "ParentDirectoriesCreated" -> count
  - "DirectoryAlreadyExisted" -> boolean
  - "PermissionsSet" -> boolean
  - "OriginalPermissions" -> string backup
- Log success with all details

**Tests**:
- Directory created successfully in new location
- Parent directories created when needed
- Existing directory skipped when AllowIfAlreadyExists=true
- Parent directories not created when CreateParentDirectories=false fails appropriately
- Permissions set correctly when configured
- Context properties populated with execution results
- Parent creation counts accurate
- Logging includes all operations

**Acceptance Criteria**:
- [ ] Directory created at correct path
- [ ] Parent directories created when needed
- [ ] State tracked for rollback logic
- [ ] Permissions applied when configured (platform-aware)
- [ ] Context properties set with results
- [ ] Logging complete and accurate
- [ ] Handles permission errors gracefully
- [ ] All execution tests pass

**Complexity**: Complex

---

### Task 1.6: Implement RollbackAsync - Permission Restoration

**Description**:
Implement rollback logic to restore original permissions if they were modified during execution.

**Dependencies**:
- Task 1.5: ExecuteAsync implemented with state tracking

**Files**:
- `/home/swelter/Projects/dotNETup/src/DotNetUp.Steps.FileSystem/CreateDirectoryStep.cs` (modify)

**Implementation Details**:
- At start, log: "Rolling back CreateDirectoryStep: {DirectoryPath}"
- Check if permissions were modified (_originalPermissions not null)
- If permissions were modified:
  - Log: "Restoring original permissions from backup"
  - Apply original permissions from backup
  - Handle platform-specific restoration (Windows ACL, Unix chmod)
  - Catch exceptions, log warning, continue (best-effort)
  - Verify restoration completed
- Return success result even if restoration fails (best-effort rollback)

**Tests**:
- Permissions restored when modified
- Restoration failures logged as warnings, don't fail rollback
- Correct platform-specific restoration applied
- Rollback succeeds even if permission restoration throws

**Acceptance Criteria**:
- [ ] Original permissions restored correctly
- [ ] Platform-specific restoration works
- [ ] Failures handled as best-effort
- [ ] Logging indicates restoration status
- [ ] Rollback completes successfully
- [ ] All permission rollback tests pass

**Complexity**: Medium

---

### Task 1.7: Implement RollbackAsync - Directory Cleanup

**Description**:
Implement logic to remove created directories and walk back parent directory chain during rollback.

**Dependencies**:
- Task 1.6: Permission restoration complete

**Files**:
- `/home/swelter/Projects/dotNETup/src/DotNetUp.Steps.FileSystem/CreateDirectoryStep.cs` (modify)

**Implementation Details**:
- After permission restoration, check if target directory was created (not pre-existing)
- If directory was created:
  - Check if directory is empty
  - If empty: delete it, log success
  - If not empty: log warning "Directory contains files/directories, leaving in place"
- Walk back parent directory chain (_parentDirectoriesCreated times):
  - Check if parent is empty
  - If empty: delete it, log "Removed empty parent directory"
  - If not empty: stop walking (other rollbacks may need it)
  - Handle locked/in-use directories: log warning, continue
- Return success result even if some cleanup fails (best-effort)

**Tests**:
- Empty created directory removed during rollback
- Non-empty created directory left in place, warning logged
- Parent directories removed if empty
- Parent removal stops at non-empty directory
- Locked directories logged as warning, rollback continues
- Rollback succeeds even if some deletion fails

**Acceptance Criteria**:
- [ ] Created directories removed when empty
- [ ] Non-empty directories preserved
- [ ] Parent directory chain cleanup works
- [ ] Locked directories handled gracefully
- [ ] Best-effort behavior maintained
- [ ] Logging complete for all cleanup actions
- [ ] All directory cleanup tests pass

**Complexity**: Medium

---

### Task 1.8: Implement RollbackAsync - Completion and Logging

**Description**:
Complete RollbackAsync with exception handling and verification of rollback success.

**Dependencies**:
- Task 1.7: Directory cleanup implemented

**Files**:
- `/home/swelter/Projects/dotNETup/src/DotNetUp.Steps.FileSystem/CreateDirectoryStep.cs` (modify)

**Implementation Details**:
- Wrap entire RollbackAsync in try-catch
- On exception: log as warning (best-effort), return failure result
- After all rollback actions:
  - Verify permissions restored (if changed)
  - Verify directories removed (if created and empty)
- Log rollback completion status
- Return InstallationStepResult indicating outcome
- Distinguish between:
  - No action needed (directory pre-existed)
  - Rollback successful (all cleanup done)
  - Rollback partial (warnings logged, best-effort completed)

**Tests**:
- Full rollback with all operations completes successfully
- Rollback exceptions caught and logged
- Final status logged appropriately
- Verification checks pass

**Acceptance Criteria**:
- [ ] Exceptions handled gracefully
- [ ] Rollback completion logged
- [ ] Verification checks in place
- [ ] Best-effort behavior maintained
- [ ] InstallationStepResult returned correctly
- [ ] All rollback tests pass

**Complexity**: Simple

---

### Task 1.9: Implement DisposeAsync for Resource Cleanup

**Description**:
Implement `DisposeAsync()` from `IAsyncDisposable` to clean up temporary resources created during execution.

**Dependencies**:
- Task 1.1-1.8: All core methods implemented

**Files**:
- `/home/swelter/Projects/dotNETup/src/DotNetUp.Steps.FileSystem/CreateDirectoryStep.cs` (modify)

**Implementation Details**:
- Following CopyFileStep pattern, DisposeAsync handles cleanup in finally block
- Cleanup responsibility: temporary permission backups, any temp files created
- Implementation:
  - Try-catch around cleanup actions
  - Silently ignore errors (best-effort, context not available for logging)
  - Call GC.SuppressFinalize(this)
  - Return ValueTask.CompletedTask
- This ensures cleanup happens:
  - After successful execution
  - After failed execution
  - Even if RollbackAsync not called (ContinueOnError scenarios)
  - In all uninstall scenarios

**Tests**:
- DisposeAsync called after execution cleans up resources
- Exceptions during disposal silently ignored
- Cleanup idempotent (safe to call multiple times)
- GC.SuppressFinalize called

**Acceptance Criteria**:
- [ ] DisposeAsync implemented
- [ ] Cleanup handles all temporary resources
- [ ] Exception handling correct
- [ ] GC.SuppressFinalize called
- [ ] All disposal tests pass

**Complexity**: Simple

---

## Phase 2: Comprehensive Unit Tests

**Goal**: Create comprehensive test suite covering success paths, failure paths, edge cases, and rollback scenarios

**Validation**: All tests pass, coverage >85%

### Task 2.1: Constructor and Property Tests

**Description**:
Write unit tests for constructor validation and property access.

**Dependencies**:
- Task 1.1: CreateDirectoryStep class exists

**Files**:
- `/home/swelter/Projects/dotNETup/tests/DotNetUp.Tests/Steps/FileSystem/CreateDirectoryStepTests.cs` (new)

**Tests**:
- Constructor with valid path sets property
- Constructor with null path throws ArgumentException with correct parameter name
- Constructor with empty path throws ArgumentException
- Constructor with whitespace path throws ArgumentException
- Name property returns "CreateDirectory"
- Description includes directory path

**Acceptance Criteria**:
- [ ] All constructor tests pass
- [ ] Property access tests pass
- [ ] Test class uses IDisposable for temp directory cleanup

**Complexity**: Simple

---

### Task 2.2: Path Validation Tests

**Description**:
Write unit tests for path validation logic in ValidateAsync.

**Dependencies**:
- Task 1.2-1.4: ValidateAsync fully implemented

**Files**:
- `/home/swelter/Projects/dotNETup/tests/DotNetUp.Tests/Steps/FileSystem/CreateDirectoryStepTests.cs` (modify)

**Tests**:
- Valid absolute path passes validation
- Valid relative path with InstallationPath resolves correctly
- Null/empty path fails with clear message
- Path with invalid characters fails appropriately
- Path exceeding OS limits fails (Windows 260 char check)
- Root device paths rejected (Windows specific)
- Parent directory validation: parent exists and writable
- Parent directory validation: parent doesn't exist with CreateParentDirectories=false fails
- Parent directory validation: parent doesn't exist with CreateParentDirectories=true passes
- Parent is a file (not directory) fails validation

**Acceptance Criteria**:
- [ ] All path validation tests pass
- [ ] Edge cases covered
- [ ] Platform-specific checks working

**Complexity**: Medium

---

### Task 2.3: Existence and Permission Validation Tests

**Description**:
Write unit tests for directory existence and permission validation.

**Dependencies**:
- Task 1.2-1.4: ValidateAsync fully implemented

**Files**:
- `/home/swelter/Projects/dotNETup/tests/DotNetUp.Tests/Steps/FileSystem/CreateDirectoryStepTests.cs` (modify)

**Tests**:
- Directory exists, AllowIfAlreadyExists=false: fails validation
- Directory exists, AllowIfAlreadyExists=true: passes validation
- Directory doesn't exist: passes validation
- Parent directory not writable: fails validation
- Parent directory writable: passes validation
- Write permission test via temp file works correctly
- Temp files from validation cleaned up

**Acceptance Criteria**:
- [ ] Existence validation correct
- [ ] Permission detection working
- [ ] Temp file cleanup working
- [ ] All tests pass

**Complexity**: Medium

---

### Task 2.4: Directory Creation Execution Tests

**Description**:
Write unit tests for successful directory creation scenarios in ExecuteAsync.

**Dependencies**:
- Task 1.5: ExecuteAsync implemented

**Files**:
- `/home/swelter/Projects/dotNETup/tests/DotNetUp.Tests/Steps/FileSystem/CreateDirectoryStepTests.cs` (modify)

**Tests**:
- Create simple directory at valid path
- Create directory with parent creation (recursive)
- Directory creation sets context properties:
  - CreatedDirectory path
  - ParentDirectoriesCreated count
  - DirectoryAlreadyExisted flag
- Pre-existing directory: execution succeeds without creation
- Verify created directory exists after execution
- Verify parent directories exist
- Logging records all operations

**Acceptance Criteria**:
- [ ] Basic directory creation works
- [ ] Parent directory creation works
- [ ] Context properties set correctly
- [ ] Pre-existing directory handled
- [ ] Logging complete
- [ ] All execution tests pass

**Complexity**: Medium

---

### Task 2.5: Permission Setting Execution Tests

**Description**:
Write unit tests for permission setting during execution.

**Dependencies**:
- Task 1.5: ExecuteAsync with permissions implemented

**Files**:
- `/home/swelter/Projects/dotNETup/tests/DotNetUp.Tests/Steps/FileSystem/CreateDirectoryStepTests.cs` (modify)

**Tests**:
- Permission setting when SetPermissions=true
- Original permissions backed up when BackupExistingPermissions=true
- Permission setting failures logged as warning, execution succeeds
- Platform-specific permission formats (Windows vs Unix)
- Invalid permission strings handled gracefully
- Owner/group setting on Unix systems (if supported)
- Permission setting skipped when SetPermissions=false
- Context.Properties includes PermissionsSet and OriginalPermissions

**Acceptance Criteria**:
- [ ] Permission setting works correctly
- [ ] Backup mechanism works
- [ ] Failures handled gracefully
- [ ] Platform differences handled
- [ ] Context properties populated
- [ ] All permission tests pass

**Complexity**: Complex

---

### Task 2.6: Rollback - Permission Restoration Tests

**Description**:
Write unit tests for permission restoration during rollback.

**Dependencies**:
- Task 1.6: Permission restoration implemented

**Files**:
- `/home/swelter/Projects/dotNETup/tests/DotNetUp.Tests/Steps/FileSystem/CreateDirectoryStepTests.cs` (modify)

**Tests**:
- Execute with permission change, then rollback restores original
- Permission restoration failures logged, rollback succeeds (best-effort)
- Verify permissions actually restored to original values
- Platform-specific restoration works

**Acceptance Criteria**:
- [ ] Permissions restored correctly
- [ ] Restoration failures handled
- [ ] Verification passes
- [ ] All rollback permission tests pass

**Complexity**: Medium

---

### Task 2.7: Rollback - Directory Cleanup Tests

**Description**:
Write unit tests for directory removal during rollback.

**Dependencies**:
- Task 1.7: Directory cleanup implemented

**Files**:
- `/home/swelter/Projects/dotNETup/tests/DotNetUp.Tests/Steps/FileSystem/CreateDirectoryStepTests.cs` (modify)

**Tests**:
- Execute creating directory, rollback removes it
- Execute creating nested directories, rollback removes them in correct order
- Pre-existing directory not removed during rollback
- Non-empty created directory left in place during rollback
- Parent directories removed if empty, stopped at non-empty
- Locked directory leaves warning, rollback continues
- Rollback succeeds even if some cleanup fails (best-effort)

**Acceptance Criteria**:
- [ ] Directory removal works
- [ ] Pre-existing directories preserved
- [ ] Parent chain cleanup correct
- [ ] Empty directory detection working
- [ ] Best-effort behavior maintained
- [ ] All rollback cleanup tests pass

**Complexity**: Medium

---

### Task 2.8: Edge Case and Error Tests

**Description**:
Write unit tests for edge cases, error conditions, and exception scenarios.

**Dependencies**:
- All previous tasks: implementation complete

**Files**:
- `/home/swelter/Projects/dotNETup/tests/DotNetUp.Tests/Steps/FileSystem/CreateDirectoryStepTests.cs` (modify)

**Tests**:
- Very deep nested paths (10+ levels)
- Special characters in path (if OS allows)
- Concurrent directory creation (if possible on test system)
- Cancellation token respected during execution
- Directory name conflicts with files
- Permission denied on parent directory
- Disk space exhaustion (if testable)
- Unicode path names
- Path with spaces
- Relative paths resolve correctly
- Special Windows paths (Program Files, AppData, etc.)

**Acceptance Criteria**:
- [ ] Edge cases handled gracefully
- [ ] Error messages clear
- [ ] No unhandled exceptions
- [ ] Cancellation respected
- [ ] All edge case tests pass

**Complexity**: Complex

---

### Task 2.9: Integration and Disposal Tests

**Description**:
Write tests for DisposeAsync, logging, and integration scenarios.

**Dependencies**:
- Task 1.9: DisposeAsync implemented

**Files**:
- `/home/swelter/Projects/dotNETup/tests/DotNetUp.Tests/Steps/FileSystem/CreateDirectoryStepTests.cs` (modify)

**Tests**:
- DisposeAsync called after execution
- DisposeAsync handles exceptions silently
- DisposeAsync idempotent (safe to call multiple times)
- Full lifecycle: validate -> execute -> rollback -> dispose
- Full lifecycle: validate -> execute -> dispose (no rollback)
- Logging at all key points
- Progress reporting during long operations
- Integration with InstallationContext properties

**Acceptance Criteria**:
- [ ] Disposal working correctly
- [ ] Full lifecycle tests pass
- [ ] Logging comprehensive
- [ ] Integration correct
- [ ] All integration tests pass

**Complexity**: Medium

---

## Phase 3: Test Fixtures and Helpers (if needed)

**Goal**: Create any custom test fixtures or helpers needed for CreateDirectoryStep tests

**Validation**: Fixtures work with existing test infrastructure

### Task 3.1: CreateDirectoryStep Test Fixture

**Description**:
If needed, create a fixture for common CreateDirectoryStep test setup (temp directories, context creation, etc.).

**Dependencies**:
- Task 2.1-2.9: Test cases identified

**Files**:
- May extend `/home/swelter/Projects/dotNETup/tests/DotNetUp.Tests/Fixtures/TestInstallationContext.cs` or create new fixture

**Implementation**:
- Base class for all CreateDirectoryStep tests providing:
  - Temp directory creation/cleanup
  - Context creation with properties
  - Mock logger configuration
  - Helper methods for assertions
- Only if tests would benefit (otherwise use inline test setup)

**Acceptance Criteria**:
- [ ] Fixture reduces test boilerplate
- [ ] Tests cleaner and more readable
- [ ] Cleanup works reliably

**Complexity**: Simple

---

## Phase 4: Documentation and Code Review

**Goal**: Document the step for users and ensure code quality

**Validation**: Documentation complete, code reviewed for patterns

### Task 4.1: XML Documentation Comments

**Description**:
Add comprehensive XML documentation comments to CreateDirectoryStep class and all public members.

**Dependencies**:
- All implementation tasks complete

**Files**:
- `/home/swelter/Projects/dotNETup/src/DotNetUp.Steps.FileSystem/CreateDirectoryStep.cs` (modify)

**Implementation**:
- Class summary: clear description of what the step does
- Constructor summary and parameter descriptions
- Property summaries
- Method summaries (ValidateAsync, ExecuteAsync, RollbackAsync)
- Notes on configuration via InstallationContext.Properties
- Platform-specific behavior notes
- Examples of configuration flags

**Acceptance Criteria**:
- [ ] All public members documented
- [ ] Documentation clear and complete
- [ ] Examples provided where helpful
- [ ] Platform notes included

**Complexity**: Simple

---

### Task 4.2: README or Usage Guide (Optional)

**Description**:
If not already present, create usage documentation showing how to use CreateDirectoryStep.

**Dependencies**:
- Task 4.1: Code documented

**Files**:
- `/home/swelter/Projects/dotNETup/src/DotNetUp.Steps.FileSystem/README.md` (new, if needed) or update existing

**Implementation**:
- Basic usage example
- Configuration options
- Error scenarios
- Rollback behavior
- Platform-specific notes

**Acceptance Criteria**:
- [ ] Usage examples clear
- [ ] Configuration documented
- [ ] Error handling explained

**Complexity**: Simple

---

## Build & Test Validation

**Goal**: Ensure everything compiles and all tests pass

**Validation**: Build succeeds, all tests green

### Task 5.1: Build Solution

**Description**:
Build the entire solution to ensure CreateDirectoryStep compiles correctly and doesn't break existing code.

**Dependencies**:
- All implementation complete

**Commands**:
```bash
cd /home/swelter/Projects/dotNETup
dotnet build
```

**Acceptance Criteria**:
- [ ] Build succeeds with zero errors
- [ ] No warnings introduced
- [ ] All projects compile

**Complexity**: Simple

---

### Task 5.2: Run All Tests

**Description**:
Run the complete test suite to ensure CreateDirectoryStep tests pass and no regressions in existing tests.

**Dependencies**:
- Task 5.1: Build succeeds

**Commands**:
```bash
cd /home/swelter/Projects/dotNETup
dotnet test
```

**Acceptance Criteria**:
- [ ] All tests pass
- [ ] CreateDirectoryStep tests at >85% coverage
- [ ] No test failures
- [ ] No regressions in existing tests

**Complexity**: Simple

---

### Task 5.3: Run Specific Step Tests

**Description**:
Run only CreateDirectoryStep tests to verify focused test coverage.

**Dependencies**:
- Task 5.1: Build succeeds

**Commands**:
```bash
cd /home/swelter/Projects/dotNETup
dotnet test --filter "FullyQualifiedName~CreateDirectoryStep"
```

**Acceptance Criteria**:
- [ ] CreateDirectoryStep tests all pass
- [ ] All test categories covered
- [ ] No failures

**Complexity**: Simple

---

## Risk Considerations

### Platform-Specific Risks

1. **Windows Path Handling**
   - Long paths (>260 chars) require \\?\ prefix
   - UNC paths \\server\share need special handling
   - Drive letters and network mounts
   - **Mitigation**: Use Path.GetFullPath(), test on Windows systems

2. **Unix Permission Model**
   - rwxrwxrwx format different from Windows ACLs
   - Owner/group availability varies
   - Umask affects actual permissions
   - **Mitigation**: Platform-specific permission handling, graceful degradation

3. **Case Sensitivity**
   - Windows: case-insensitive
   - Unix: case-sensitive
   - **Mitigation**: Document this difference, test both

### Concurrency Risks

1. **Race Conditions**
   - Directory created by another process between validation and execution
   - **Mitigation**: Check again in ExecuteAsync, handle already-created case

2. **Permission Changes**
   - Parent directory permissions changed by another process
   - **Mitigation**: Log warnings, continue best-effort

3. **In-Use Directories**
   - Directory locked during rollback cleanup
   - **Mitigation**: Log warning, leave directory, other step rollbacks may need it

### Error Handling Risks

1. **Permission Errors**
   - Insufficient privileges to set permissions
   - User/group doesn't exist
   - **Mitigation**: Log as warning, continue, don't fail execution

2. **Disk Space**
   - Device full during creation
   - **Mitigation**: Validate space during ValidateAsync

3. **Locked/In-Use Directories**
   - Cannot delete during rollback
   - **Mitigation**: Log warning, continue best-effort

### Testing Risks

1. **Platform-Specific Test Failures**
   - Tests may behave differently on Windows vs Unix
   - **Mitigation**: Conditional tests, platform-specific assertions

2. **Temporary File Conflicts**
   - Temp files from tests conflict if run in parallel
   - **Mitigation**: Use unique GUIDs, separate test directories

3. **Cleanup Failures**
   - Tests may leave orphaned directories
   - **Mitigation**: Use IDisposable, cleanup in finalizers

## Dependencies Between Tasks

```
Task 1.1 (Class Structure)
  ├─> Task 1.2 (Path Validation)
  │     └─> Task 1.3 (Existence/Parent Checks)
  │           └─> Task 1.4 (Validation Complete)
  │                 └─> Task 1.5 (ExecuteAsync)
  │                       └─> Task 1.6 (Rollback Permissions)
  │                             └─> Task 1.7 (Rollback Cleanup)
  │                                   └─> Task 1.8 (Rollback Complete)
  │                                         └─> Task 1.9 (DisposeAsync)
  │
  └─> Task 2.1 (Constructor Tests)
        └─> Task 2.2 (Path Validation Tests)
              └─> Task 2.3 (Existence Tests)
                    └─> Task 2.4 (Execution Tests)
                          └─> Task 2.5 (Permission Tests)
                                └─> Task 2.6 (Rollback Permission Tests)
                                      └─> Task 2.7 (Rollback Cleanup Tests)
                                            └─> Task 2.8 (Edge Case Tests)
                                                  └─> Task 2.9 (Integration Tests)

  └─> Task 4.1 (XML Docs)
        └─> Task 4.2 (README)

  └─> Task 5.1 (Build)
        └─> Task 5.2 (Full Tests)
        └─> Task 5.3 (Focused Tests)
```

## Build Order Summary

Recommended implementation order:

1. **Task 1.1** - CreateDirectoryStep class structure
2. **Task 2.1** - Constructor tests (verify basic structure works)
3. **Task 1.2** - Path validation
4. **Task 2.2** - Path validation tests
5. **Task 1.3** - Existence/parent checks
6. **Task 2.3** - Existence tests
7. **Task 1.4** - Validation logging and completion
8. **Task 1.5** - ExecuteAsync implementation
9. **Task 2.4** - Execution tests
10. **Task 2.5** - Permission setting tests
11. **Task 1.6** - Rollback permission restoration
12. **Task 2.6** - Rollback permission tests
13. **Task 1.7** - Rollback directory cleanup
14. **Task 2.7** - Rollback cleanup tests
15. **Task 1.8** - Rollback completion
16. **Task 1.9** - DisposeAsync
17. **Task 2.8** - Edge case tests
18. **Task 2.9** - Integration tests
19. **Task 3.1** - Test fixtures (if needed)
20. **Task 4.1** - XML documentation
21. **Task 4.2** - README (optional)
22. **Task 5.1** - Build solution
23. **Task 5.2** - Run all tests
24. **Task 5.3** - Run focused tests

## Implementation Notes

### File Locations

All files will be in the existing DotNetUp.Steps.FileSystem project structure:

**Implementation**:
- `/home/swelter/Projects/dotNETup/src/DotNetUp.Steps.FileSystem/CreateDirectoryStep.cs`

**Tests**:
- `/home/swelter/Projects/dotNETup/tests/DotNetUp.Tests/Steps/FileSystem/CreateDirectoryStepTests.cs`

### Naming Conventions

Following existing codebase patterns:
- Class name: `CreateDirectoryStep`
- Public properties: `DirectoryPath`
- Private fields: `_directoryExistedBefore`, `_parentDirectoriesCreated`, etc. (camelCase with underscore)
- Constants: `PascalCase`
- Methods: `ValidateAsync`, `ExecuteAsync`, `RollbackAsync` (following IInstallationStep)

### Configuration via Context.Properties

Steps in this codebase typically read configuration from `InstallationContext.Properties` dictionary:

```csharp
// Example configuration:
context.Properties["DirectoryPath"] = "/app/config";
context.Properties["CreateParentDirectories"] = true;
context.Properties["AllowIfAlreadyExists"] = false;
context.Properties["SetPermissions"] = true;
context.Properties["Permissions"] = "755";  // Unix-style
// or "EVERYONE:Modify" on Windows
```

The step should extract these with sensible defaults.

### Testing Strategy

- **Unit Tests**: Mock file system operations if needed, use real temp directories
- **Integration Tests**: Use real file system with isolated temp directories
- **Edge Cases**: Test path length limits, special characters, concurrency
- **Rollback**: Always test execute->rollback->verify cycle

### Error Messages

Follow the specification's error message format with clarity:
- Include the actual path or value that caused the error
- Use present tense: "Directory path too long" not "was too long"
- Be specific about the remediation: "Set AllowIfAlreadyExists to true to allow"

