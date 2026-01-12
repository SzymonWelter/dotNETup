# CreateDirectoryStep - Task Dependency & Reference Guide

## Task Dependency Graph

### Phase 1: Implementation Tasks (1.1-1.9)

```
                          ┌─────────────────────────────────────┐
                          │   Task 1.1                          │
                          │   Class Structure & Constructor     │
                          │   - Define DirectoryPath property   │
                          │   - Implement Name, Description     │
                          │   - Constructor validation          │
                          └──────────────┬──────────────────────┘
                                         │
                    ┌────────────────────┴────────────────────┐
                    │                                         │
         ┌──────────▼──────────────┐           ┌─────────────▼──────────┐
         │   Task 1.2              │           │   Task 2.1             │
         │   Path Validation       │           │   Constructor Tests    │
         │   - Path length checks  │           │   (Test Phase)         │
         │   - Invalid chars       │           │                        │
         │   - Device paths        │           │                        │
         └──────────┬──────────────┘           └────────────────────────┘
                    │
         ┌──────────▼──────────────┐
         │   Task 1.3              │
         │   Existence/Parent      │
         │   - Directory existence │
         │   - Parent validation   │
         │   - Write permissions   │
         └──────────┬──────────────┘
                    │
         ┌──────────▼──────────────┐
         │   Task 1.4              │
         │   Validation Logging    │
         │   - Add logging         │
         │   - Handle exceptions   │
         │   - Cleanup temp files  │
         └──────────┬──────────────┘
                    │
         ┌──────────▼──────────────┐
         │   Task 1.5              │
         │   ExecuteAsync          │
         │   - Create directories  │
         │   - Parent creation     │
         │   - Set permissions     │
         │   - Track state         │
         │   - Populate context    │
         └──────────┬──────────────┘
                    │
         ┌──────────▼──────────────┐
         │   Task 1.6              │
         │   Rollback Perms        │
         │   - Restore perms       │
         │   - Platform-specific   │
         │   - Best-effort         │
         └──────────┬──────────────┘
                    │
         ┌──────────▼──────────────┐
         │   Task 1.7              │
         │   Rollback Cleanup      │
         │   - Remove dirs         │
         │   - Parent chain        │
         │   - Handle locked dirs  │
         └──────────┬──────────────┘
                    │
         ┌──────────▼──────────────┐
         │   Task 1.8              │
         │   Rollback Completion   │
         │   - Exception handling  │
         │   - Verification        │
         │   - Logging             │
         └──────────┬──────────────┘
                    │
         ┌──────────▼──────────────┐
         │   Task 1.9              │
         │   DisposeAsync          │
         │   - Cleanup resources   │
         │   - Temp files          │
         │   - Idempotent          │
         └──────────────────────────┘
```

### Phase 2: Test Tasks (2.1-2.9)

```
         ┌─────────────────────────┐
         │   Task 2.1              │
         │   Constructor Tests     │
         │   - Valid paths         │
         │   - Null/empty input    │
         │   - Property access     │
         └──────────┬──────────────┘
                    │
         ┌──────────▼──────────────┐
         │   Task 2.2              │
         │   Path Validation Tests │
         │   - Valid paths         │
         │   - Invalid paths       │
         │   - Path limits         │
         │   - Special paths       │
         └──────────┬──────────────┘
                    │
         ┌──────────▼──────────────┐
         │   Task 2.3              │
         │   Existence Tests       │
         │   - Existing dirs       │
         │   - Parent validation   │
         │   - Write permissions   │
         └──────────┬──────────────┘
                    │
         ┌──────────▼──────────────┐
         │   Task 2.4              │
         │   Execution Tests       │
         │   - Create directory    │
         │   - Parent creation     │
         │   - Pre-existing        │
         │   - Context properties  │
         └──────────┬──────────────┘
                    │
         ┌──────────▼──────────────┐
         │   Task 2.5              │
         │   Permission Tests      │
         │   - Setting perms       │
         │   - Backup/restore      │
         │   - Platform-specific   │
         │   - Failure handling    │
         └──────────┬──────────────┘
                    │
         ┌──────────▼──────────────┐
         │   Task 2.6              │
         │   Rollback Perm Tests   │
         │   - Restoration         │
         │   - Failure handling    │
         │   - Verification        │
         └──────────┬──────────────┘
                    │
         ┌──────────▼──────────────┐
         │   Task 2.7              │
         │   Rollback Cleanup Tests│
         │   - Directory removal   │
         │   - Parent chain        │
         │   - Locked dirs         │
         │   - Pre-existing        │
         └──────────┬──────────────┘
                    │
         ┌──────────▼──────────────┐
         │   Task 2.8              │
         │   Edge Case Tests       │
         │   - Deep paths          │
         │   - Special chars       │
         │   - Concurrency         │
         │   - Disk space          │
         └──────────┬──────────────┘
                    │
         ┌──────────▼──────────────┐
         │   Task 2.9              │
         │   Integration Tests     │
         │   - Full lifecycle      │
         │   - Logging             │
         │   - Disposal            │
         │   - Progress reporting  │
         └──────────────────────────┘
```

---

## Implementation Task Checklist

### Phase 1: Core Implementation

- [ ] **Task 1.1: Class Structure** (Simple)
  - [ ] Create CreateDirectoryStep.cs
  - [ ] Inherit from IInstallationStep
  - [ ] Define DirectoryPath property
  - [ ] Implement Name and Description
  - [ ] Create constructor with validation
  - **Time Estimate**: 30 minutes
  - **Tests**: Run constructor tests (Task 2.1)

- [ ] **Task 1.2: Path Validation** (Medium)
  - [ ] Path normalization with Path.GetFullPath()
  - [ ] OS path length validation
  - [ ] Invalid character detection
  - [ ] Special path detection (device paths, network paths)
  - [ ] Comprehensive error messages
  - **Time Estimate**: 1 hour
  - **Tests**: Run path validation tests (Task 2.2)

- [ ] **Task 1.3: Existence/Parent Checks** (Medium)
  - [ ] Directory existence detection
  - [ ] AllowIfAlreadyExists flag handling
  - [ ] Parent directory validation
  - [ ] Write permission checking (temp file method)
  - [ ] CreateParentDirectories flag logic
  - **Time Estimate**: 1 hour
  - **Tests**: Run existence tests (Task 2.3)

- [ ] **Task 1.4: Validation Complete** (Simple)
  - [ ] Add comprehensive logging
  - [ ] Exception handling wrapper
  - [ ] Temp file cleanup
  - [ ] Return InstallationStepResult
  - **Time Estimate**: 30 minutes
  - **Tests**: Run full validation tests

- [ ] **Task 1.5: ExecuteAsync Implementation** (Complex)
  - [ ] State field declarations
  - [ ] Configuration extraction
  - [ ] Directory existence pre-check
  - [ ] Parent directory creation
  - [ ] Target directory creation
  - [ ] Permission setting with platform awareness
  - [ ] Context.Properties population
  - [ ] Comprehensive logging
  - **Time Estimate**: 2 hours
  - **Tests**: Run execution tests (Task 2.4-2.5)

- [ ] **Task 1.6: Rollback Permissions** (Medium)
  - [ ] Permission restoration logic
  - [ ] Platform-specific restoration
  - [ ] Exception handling (best-effort)
  - [ ] Logging restoration status
  - **Time Estimate**: 1 hour
  - **Tests**: Run rollback permission tests (Task 2.6)

- [ ] **Task 1.7: Rollback Cleanup** (Medium)
  - [ ] Target directory removal
  - [ ] Empty directory detection
  - [ ] Parent chain cleanup
  - [ ] Locked directory handling
  - [ ] Logging all cleanup actions
  - **Time Estimate**: 1 hour
  - **Tests**: Run rollback cleanup tests (Task 2.7)

- [ ] **Task 1.8: Rollback Complete** (Simple)
  - [ ] Exception handling wrapper
  - [ ] Verification checks
  - [ ] Logging completion status
  - [ ] Return InstallationStepResult
  - **Time Estimate**: 30 minutes
  - **Tests**: Run full rollback tests

- [ ] **Task 1.9: DisposeAsync** (Simple)
  - [ ] Resource cleanup implementation
  - [ ] Exception handling (silent)
  - [ ] Temp file cleanup
  - [ ] GC.SuppressFinalize call
  - **Time Estimate**: 30 minutes
  - **Tests**: Run disposal tests (Task 2.9)

---

### Phase 2: Testing

- [ ] **Task 2.1: Constructor Tests** (Simple)
  - [ ] Create CreateDirectoryStepTests.cs
  - [ ] Write 5-6 test cases
  - [ ] IDisposable for cleanup
  - **Time Estimate**: 1 hour
  - **Coverage Target**: 100% constructor code

- [ ] **Task 2.2: Path Validation Tests** (Medium)
  - [ ] Write 8-10 test cases
  - [ ] Cover all validation branches
  - [ ] Test error messages
  - **Time Estimate**: 1.5 hours
  - **Coverage Target**: 95%+ path validation code

- [ ] **Task 2.3: Existence/Parent Tests** (Medium)
  - [ ] Write 7-8 test cases
  - [ ] Test all flag combinations
  - [ ] Verify temp file cleanup
  - **Time Estimate**: 1.5 hours
  - **Coverage Target**: 95%+ existence code

- [ ] **Task 2.4: Execution Tests** (Medium)
  - [ ] Write 8-10 test cases
  - [ ] Test directory creation
  - [ ] Test parent creation
  - [ ] Verify context properties
  - **Time Estimate**: 1.5 hours
  - **Coverage Target**: 90%+ execution code

- [ ] **Task 2.5: Permission Tests** (Medium)
  - [ ] Write 8-10 test cases
  - [ ] Test permission setting
  - [ ] Test backup/restore
  - [ ] Test platform-specific behavior
  - **Time Estimate**: 1.5 hours
  - **Coverage Target**: 85%+ permission code

- [ ] **Task 2.6: Rollback Permission Tests** (Medium)
  - [ ] Write 5-6 test cases
  - [ ] Test restoration scenarios
  - [ ] Test failure handling
  - **Time Estimate**: 1 hour
  - **Coverage Target**: 90%+ rollback permission code

- [ ] **Task 2.7: Rollback Cleanup Tests** (Medium)
  - [ ] Write 8-10 test cases
  - [ ] Test directory removal
  - [ ] Test parent chain cleanup
  - [ ] Test edge cases
  - **Time Estimate**: 1.5 hours
  - **Coverage Target**: 90%+ rollback cleanup code

- [ ] **Task 2.8: Edge Case Tests** (Complex)
  - [ ] Write 10-12 test cases
  - [ ] Deep path nesting
  - [ ] Special characters
  - [ ] Concurrency scenarios
  - [ ] Disk space scenarios
  - **Time Estimate**: 2 hours
  - **Coverage Target**: Edge case branches

- [ ] **Task 2.9: Integration Tests** (Medium)
  - [ ] Write 6-8 test cases
  - [ ] Full lifecycle: validate→execute→rollback→dispose
  - [ ] Logging verification
  - [ ] Progress reporting
  - **Time Estimate**: 1.5 hours
  - **Coverage Target**: Integration paths

---

### Phase 3: Optional Fixtures

- [ ] **Task 3.1: Test Fixtures** (Simple)
  - [ ] Create custom fixture class (if needed)
  - [ ] Reduce test boilerplate
  - **Time Estimate**: 1 hour (optional)

---

### Phase 4: Documentation

- [ ] **Task 4.1: XML Documentation** (Simple)
  - [ ] Class-level summary
  - [ ] Property documentation
  - [ ] Method documentation
  - [ ] Parameter descriptions
  - [ ] Platform notes
  - **Time Estimate**: 1 hour

- [ ] **Task 4.2: Usage Guide** (Simple)
  - [ ] Basic usage examples
  - [ ] Configuration options
  - [ ] Error handling
  - [ ] Rollback behavior
  - **Time Estimate**: 1 hour (optional)

---

### Phase 5: Build & Validation

- [ ] **Task 5.1: Build Solution**
  - Command: `dotnet build`
  - Expected: Zero errors
  - **Time Estimate**: 5 minutes

- [ ] **Task 5.2: Run All Tests**
  - Command: `dotnet test`
  - Expected: All tests pass
  - **Time Estimate**: 5-10 minutes

- [ ] **Task 5.3: Run Focused Tests**
  - Command: `dotnet test --filter "FullyQualifiedName~CreateDirectoryStep"`
  - Expected: All CreateDirectoryStep tests pass
  - **Time Estimate**: 2-3 minutes

---

## Total Time Estimates

| Phase | Component | Time |
|-------|-----------|------|
| **Phase 1** | Task 1.1 | 0.5 hrs |
| | Task 1.2 | 1.0 hrs |
| | Task 1.3 | 1.0 hrs |
| | Task 1.4 | 0.5 hrs |
| | Task 1.5 | 2.0 hrs |
| | Task 1.6 | 1.0 hrs |
| | Task 1.7 | 1.0 hrs |
| | Task 1.8 | 0.5 hrs |
| | Task 1.9 | 0.5 hrs |
| | **Phase 1 Total** | **8.0 hrs** |
| **Phase 2** | Task 2.1 | 1.0 hrs |
| | Task 2.2 | 1.5 hrs |
| | Task 2.3 | 1.5 hrs |
| | Task 2.4 | 1.5 hrs |
| | Task 2.5 | 1.5 hrs |
| | Task 2.6 | 1.0 hrs |
| | Task 2.7 | 1.5 hrs |
| | Task 2.8 | 2.0 hrs |
| | Task 2.9 | 1.5 hrs |
| | **Phase 2 Total** | **13.5 hrs** |
| **Phase 3** | Task 3.1 (optional) | 1.0 hrs |
| **Phase 4** | Task 4.1 | 1.0 hrs |
| | Task 4.2 (optional) | 1.0 hrs |
| | **Phase 4 Total** | **2.0 hrs** |
| **Phase 5** | Task 5.1-5.3 | 0.25 hrs |
| | **Grand Total** | **~24-25 hrs** |

*Note: Times are estimates and may vary based on experience level and complexity of platform-specific handling*

---

## Success Metrics

| Metric | Target |
|--------|--------|
| **Code Compilation** | Zero errors, zero warnings |
| **Test Coverage** | >85% overall |
| **Test Count** | 50-70 tests |
| **Constructor Code** | 100% coverage |
| **Path Validation** | 95%+ coverage |
| **Execution** | 90%+ coverage |
| **Rollback** | 90%+ coverage |
| **All Tests Pass** | 100% |
| **No Regressions** | All existing tests still pass |
| **Implementation LOC** | 400-500 lines |
| **Test LOC** | 800-1000 lines |

---

## Common Pitfalls to Avoid

1. **Forgetting state tracking** - Must track _directoryExistedBefore, _parentDirectoriesCreated, etc. for rollback
2. **Not handling pre-existing directories** - ExecuteAsync must return success if AllowIfAlreadyExists=true
3. **Incomplete rollback** - Must restore permissions AND remove directories
4. **Missing temp file cleanup** - Validation temp files must be cleaned up in all paths
5. **Not checking for locked directories** - Rollback should log warning and continue, not fail
6. **Ignoring platform differences** - Windows permissions differ from Unix, must handle both
7. **Insufficient logging** - Every decision point should be logged for debugging
8. **Catching too broadly** - Catch specific exceptions, not generic Exception where possible
9. **Not testing edge cases** - Very deep paths, special characters, Unicode names
10. **Forgetting DisposeAsync** - Resource cleanup may cause orphaned backups if missing

---

## File References

**Implementation**:
- Location: `/home/swelter/Projects/dotNETup/src/DotNetUp.Steps.FileSystem/CreateDirectoryStep.cs`
- Reference: `CopyFileStep.cs` for patterns

**Tests**:
- Location: `/home/swelter/Projects/dotNETup/tests/DotNetUp.Tests/Steps/FileSystem/CreateDirectoryStepTests.cs`
- Reference: `CopyFileStepTests.cs` for patterns

**Core Interfaces**:
- `IInstallationStep`: `/home/swelter/Projects/dotNETup/src/DotNetUp.Core/Interfaces/IInstallationStep.cs`
- `InstallationContext`: `/home/swelter/Projects/dotNETup/src/DotNetUp.Core/Models/InstallationContext.cs`
- `InstallationStepResult`: `/home/swelter/Projects/dotNETup/src/DotNetUp.Core/Models/InstallationStepResult.cs`

**Test Fixtures**:
- `TestInstallationContext`: `/home/swelter/Projects/dotNETup/tests/DotNetUp.Tests/Fixtures/TestInstallationContext.cs`

---

## Quick Start Commands

```bash
# Navigate to project
cd /home/swelter/Projects/dotNETup

# Build project (after Task 1.9)
dotnet build

# Run all tests (after Task 2.9)
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~CreateDirectoryStepTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~CreateDirectoryStepTests.Constructor_WithValidPath"

# Run with verbose output
dotnet test --verbosity normal

# List available tests
dotnet test --list-tests --filter "FullyQualifiedName~CreateDirectoryStep"
```

---

**Created**: 2026-01-12
**Status**: Ready for Implementation
**Questions?**: Refer to full plan at `/home/swelter/Projects/dotNETup/IMPLEMENTATION_PLAN_CREATE_DIRECTORY_STEP.md`
