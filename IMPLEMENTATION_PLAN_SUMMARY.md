# CreateDirectoryStep Implementation Plan - Executive Summary

## Quick Reference

**Implementation File**: `/home/swelter/Projects/dotNETup/src/DotNetUp.Steps.FileSystem/CreateDirectoryStep.cs`
**Test File**: `/home/swelter/Projects/dotNETup/tests/DotNetUp.Tests/Steps/FileSystem/CreateDirectoryStepTests.cs`
**Full Plan**: `/home/swelter/Projects/dotNETup/IMPLEMENTATION_PLAN_CREATE_DIRECTORY_STEP.md`

---

## Overview

The **CreateDirectoryStep** is a foundational installation step that creates directory structures with support for:

- Parent directory creation (recursive)
- Pre-existing directory handling
- Permission configuration (platform-aware)
- Automatic rollback with cleanup
- Comprehensive logging
- Best-effort failure handling

This step implements the `IInstallationStep` interface like `CopyFileStep`, following established patterns in the codebase.

---

## Key Features

| Feature | Details |
|---------|---------|
| **Parent Creation** | Recursively create parent directories with tracking |
| **Permission Control** | Set permissions (Windows ACLs, Unix chmod) with rollback |
| **Existence Handling** | Skip if exists (with `AllowIfAlreadyExists` flag) |
| **Rollback** | Removes created directories, restores permissions (best-effort) |
| **Cross-Platform** | Handles Windows and Unix-like systems appropriately |
| **Error Recovery** | Graceful degradation with logging (e.g., permission failures logged as warnings) |

---

## Implementation Phases

### Phase 1: Core Step Implementation (Tasks 1.1-1.9)
Implement the `CreateDirectoryStep` class with all required methods:
- **Task 1.1**: Class structure and properties
- **Task 1.2-1.4**: ValidateAsync (path, existence, parent checks, logging)
- **Task 1.5**: ExecuteAsync (directory creation, permissions, state tracking)
- **Task 1.6-1.8**: RollbackAsync (permission restoration, directory cleanup)
- **Task 1.9**: DisposeAsync (resource cleanup)

**Complexity**: Complex (9 focused tasks)
**Estimated Lines**: 400-500 LOC for implementation

### Phase 2: Comprehensive Unit Tests (Tasks 2.1-2.9)
Write full test coverage for all scenarios:
- **Task 2.1**: Constructor and property tests
- **Task 2.2**: Path validation tests
- **Task 2.3**: Existence and permission validation tests
- **Task 2.4**: Directory creation execution tests
- **Task 2.5**: Permission setting execution tests
- **Task 2.6**: Rollback permission restoration tests
- **Task 2.7**: Rollback directory cleanup tests
- **Task 2.8**: Edge case and error tests
- **Task 2.9**: Integration and disposal tests

**Complexity**: Complex (9 focused test modules)
**Estimated Tests**: 50-70 test cases
**Target Coverage**: >85%

### Phase 3: Test Fixtures (Task 3.1 - Optional)
Create custom test fixtures if needed to reduce boilerplate.

**Complexity**: Simple
**Only if** tests would significantly benefit

### Phase 4: Documentation (Tasks 4.1-4.2)
Add XML documentation and usage guide.

**Complexity**: Simple
**Estimated**: 100-150 lines of documentation

### Phase 5: Build & Validation (Tasks 5.1-5.3)
Build solution and run all tests to verify everything works.

**Complexity**: Simple
**Commands**: `dotnet build`, `dotnet test`

---

## Critical Implementation Details

### Configuration via InstallationContext.Properties

The step reads configuration from the context properties dictionary:

```csharp
// Example usage in installation definition:
context.Properties["DirectoryPath"] = "/app/config";
context.Properties["CreateParentDirectories"] = true;
context.Properties["AllowIfAlreadyExists"] = false;
context.Properties["SetPermissions"] = true;
context.Properties["Permissions"] = "755";
```

**Required Parameters**:
- `DirectoryPath` (string) - Full path to directory

**Optional Parameters** (with defaults):
- `CreateParentDirectories` (bool) = true
- `AllowIfAlreadyExists` (bool) = false
- `SetPermissions` (bool) = false
- `Permissions` (string) = null
- `Owner` (string) = null
- `Group` (string) = null
- `BackupExistingPermissions` (bool) = false

### State Tracking for Rollback

The step maintains internal state to guide rollback:

```csharp
private bool _directoryExistedBefore;        // Was it pre-existing?
private int _parentDirectoriesCreated;       // How many parents created?
private string? _originalPermissions;        // Backup of original perms
private bool _creationSucceeded;            // Did creation complete?
```

This allows rollback to:
1. Skip removal if directory pre-existed
2. Walk back parent chain by correct count
3. Restore original permissions
4. Handle partial failures gracefully

### Context Properties Result Keys

After execution, the step populates context properties with results:

```csharp
context.Properties["CreatedDirectory"] = "/app/config";           // Resolved path
context.Properties["ParentDirectoriesCreated"] = 3;               // Count
context.Properties["DirectoryAlreadyExisted"] = false;            // Was it pre-existing?
context.Properties["PermissionsSet"] = true;                      // Were perms set?
context.Properties["OriginalPermissions"] = "drwxr-xr-x";        // Backup
```

Other steps can read these values for subsequent operations.

### Rollback Strategy

Rollback follows a specific order:
1. **Restore Permissions** - If permissions were modified
2. **Remove Target Directory** - Only if created and now empty
3. **Remove Parent Chain** - Remove empty parents up to pre-existing ancestor
4. **Handle Failures** - Log warnings, continue (best-effort)

Example: If execution created `/app/config/db` with 2 parent directories:
- Rollback removes `/app/config/db` (if empty)
- Rollback removes `/app/config` (if empty)
- Rollback removes `/app` (if empty and was created)
- Rollback stops at pre-existing ancestor

---

## Testing Strategy

### Test Structure

Each test class should follow the CopyFileStep test pattern:

```csharp
public class CreateDirectoryStepTests : IDisposable
{
    private readonly string _testDir;

    public CreateDirectoryStepTests()
    {
        // Create unique temp directory for test isolation
        _testDir = Path.Combine(Path.GetTempPath(),
            $"DotNetUpTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        // Clean up after test
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    // Tests here...
}
```

### Test Categories

| Category | Test Count | Purpose |
|----------|-----------|---------|
| Constructor | 5 | Validate input parameters |
| Path Validation | 8 | Ensure paths are valid |
| Existence/Parent | 7 | Check directory and parent states |
| Execution | 8 | Verify directory creation |
| Permissions | 8 | Test permission setting and restoration |
| Rollback | 10 | Verify cleanup and restoration |
| Edge Cases | 10 | Handle unusual scenarios |
| Integration | 6 | Full lifecycle scenarios |
| **Total** | **~60 tests** | **>85% coverage** |

### Key Test Scenarios

**Happy Path**:
- Create simple directory
- Create directory with parents
- Create with permission setting

**Existing Directory**:
- Exists, AllowIfAlreadyExists=false → failure
- Exists, AllowIfAlreadyExists=true → success (skip)

**Rollback Scenarios**:
- Execute → Rollback → Verify cleanup
- Permissions changed → Rollback → Verify restored
- Parents created → Rollback → Verify removed

**Error Cases**:
- Invalid path
- Insufficient permissions
- Path too long
- Parent is file (not directory)

---

## Dependencies & Order

```
Implementation Phase 1 (Tasks 1.1-1.9)
    ├─ 1.1: Class structure
    ├─ 1.2-1.4: ValidateAsync
    ├─ 1.5: ExecuteAsync
    ├─ 1.6-1.8: RollbackAsync
    └─ 1.9: DisposeAsync

Test Phase 2 (Tasks 2.1-2.9)
    ├─ 2.1: Constructor tests
    ├─ 2.2-2.3: Validation tests
    ├─ 2.4-2.5: Execution tests
    ├─ 2.6-2.7: Rollback tests
    ├─ 2.8: Edge cases
    └─ 2.9: Integration

Documentation Phase 4 (Tasks 4.1-4.2)
    ├─ 4.1: XML docs
    └─ 4.2: Usage guide

Build Validation Phase 5 (Tasks 5.1-5.3)
    ├─ 5.1: dotnet build
    ├─ 5.2: dotnet test
    └─ 5.3: Focused tests
```

**Recommended Order**: 1.1 → 2.1 → 1.2-1.5 → 2.2-2.5 → 1.6-1.9 → 2.6-2.9 → 2.8 → 4.1-4.2 → 5.1-5.3

---

## Risk Mitigation

| Risk | Mitigation |
|------|-----------|
| **Windows long paths** | Use `Path.GetFullPath()`, handle \\?\\ prefix |
| **Permission differences** | Platform-specific permission handling, graceful degradation |
| **Race conditions** | Check again in ExecuteAsync, handle already-created case |
| **Locked directories** | Log warning, leave in place, continue (best-effort) |
| **Concurrent operations** | Use unique GUIDs for temp files, isolated test directories |
| **Permission errors** | Log as warning, don't fail execution (except for required paths) |

---

## Success Criteria - Completion Checklist

**Implementation (Phase 1)**:
- [ ] CreateDirectoryStep.cs created with all required methods
- [ ] Implements IInstallationStep interface
- [ ] ValidateAsync checks path, existence, parent, permissions
- [ ] ExecuteAsync creates directories, sets permissions, tracks state
- [ ] RollbackAsync restores permissions, cleans up directories
- [ ] DisposeAsync handles resource cleanup
- [ ] Code compiles without errors
- [ ] XML documentation complete

**Testing (Phase 2)**:
- [ ] CreateDirectoryStepTests.cs created
- [ ] 50-70 test cases covering all scenarios
- [ ] >85% code coverage
- [ ] All tests pass locally
- [ ] Constructor tests pass
- [ ] Path validation tests pass
- [ ] Execution tests pass
- [ ] Rollback tests pass
- [ ] Edge case tests pass
- [ ] Integration tests pass

**Build & Validation (Phase 5)**:
- [ ] `dotnet build` succeeds (zero errors)
- [ ] `dotnet test` succeeds (all tests pass)
- [ ] No regressions in existing tests
- [ ] CreateDirectoryStep specific tests all pass

---

## Quick Start

1. **Read the full plan**: `/home/swelter/Projects/dotNETup/IMPLEMENTATION_PLAN_CREATE_DIRECTORY_STEP.md`
2. **Start with Task 1.1**: Create the class structure
3. **Parallelize**: Write constructor tests (2.1) while implementing
4. **Follow the dependency chain** in build order summary
5. **Build and test frequently** after each task
6. **Reference CopyFileStep** for patterns and conventions

---

## Files to Create/Modify

**Create**:
- `/home/swelter/Projects/dotNETup/src/DotNetUp.Steps.FileSystem/CreateDirectoryStep.cs`
- `/home/swelter/Projects/dotNETup/tests/DotNetUp.Tests/Steps/FileSystem/CreateDirectoryStepTests.cs`

**May Create**:
- Test fixture (Task 3.1) - if beneficial
- README.md (Task 4.2) - if not already present

**No Changes Needed**:
- IInstallationStep interface
- InstallationContext
- InstallationStepResult
- Existing test infrastructure

---

## Key Commands

```bash
# Build solution
dotnet build

# Run all tests
dotnet test

# Run CreateDirectoryStep tests only
dotnet test --filter "FullyQualifiedName~CreateDirectoryStep"

# Run with verbose output
dotnet test --verbosity normal

# Run specific test method
dotnet test --filter "FullyQualifiedName~CreateDirectoryStepTests.Constructor"
```

---

## Expected Outcomes

After implementation completion:

**Functionality**:
- Directories created at specified paths
- Parent directories created recursively
- Permissions set and managed
- Complete automatic rollback on failure
- Cross-platform support (Windows/Unix)
- Comprehensive error messages

**Code Quality**:
- 400-500 LOC for implementation
- 50-70 test cases
- >85% test coverage
- Zero compiler warnings
- Follows established codebase patterns

**Documentation**:
- XML documentation comments
- Usage examples in comments
- Error handling explained
- Platform-specific notes included

---

**Last Updated**: 2026-01-12
**Status**: Ready for Implementation
**Total Tasks**: 25 (9 implementation + 9 tests + 1 fixture + 2 docs + 3 validation)
