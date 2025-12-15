# Core Executor Implementation Plan

**Iteration Goal:** Implement Installation Executor with enhanced progress reporting
**Approach:** Test-First Development (TDD)
**Testing Stack:** xUnit + NSubstitute + FluentAssertions
**Target Branch:** `claude/review-and-plan-k14z5`
**Status:** Ready for Implementation

---

## Overview

This plan implements the core orchestration engine for DotNetUp - the Installation executor that validates, executes, and rolls back installation steps. It also introduces structured progress reporting to replace the simple string-based system.

**Key Deliverables:**
1. InstallationProgress model for structured progress reporting
2. Enhanced InstallationContext with progress management
3. Installation executor with validation, execution, and rollback
4. Comprehensive test suite (80%+ coverage)
5. Testing documentation and fixtures

**Estimated Completion:** 40% → 75% of Phase 1 MVP

---

## Implementation Order (15 Files)

### 📋 PHASE 1: Progress Reporting Infrastructure

#### 1. `src/DotNetUp.Core/Models/InstallationProgress.cs` ✨ NEW

**Purpose:** Data structure for structured progress reporting

**Contents:**
```csharp
namespace DotNetUp.Core.Models;

/// <summary>
/// Represents the current progress of an installation.
/// Combines executor-level tracking (step number) with step-level details (substep, percentage).
/// </summary>
public class InstallationProgress
{
    /// <summary>
    /// Current step number (1-based). Set by executor.
    /// </summary>
    public int CurrentStepNumber { get; init; }

    /// <summary>
    /// Total number of steps in the installation. Set by executor.
    /// </summary>
    public int TotalSteps { get; init; }

    /// <summary>
    /// Name of the current step (from IInstallationStep.Name). Set by executor.
    /// </summary>
    public string CurrentStepName { get; init; } = string.Empty;

    /// <summary>
    /// Description of the current substep within the step. Set by the step itself.
    /// Example: "Backing up existing file", "Copying file", "Setting permissions"
    /// </summary>
    public string? SubStepDescription { get; init; }

    /// <summary>
    /// Completion percentage of the current step (0-100). Set by the step itself.
    /// </summary>
    public int PercentComplete { get; init; }

    /// <summary>
    /// Calculates overall installation progress across all steps.
    /// Formula: ((completed_steps * 100) + current_step_percent) / total_steps
    /// </summary>
    public double OverallPercentComplete =>
        TotalSteps > 0
            ? ((CurrentStepNumber - 1) * 100.0 + PercentComplete) / TotalSteps
            : 0;
}
```

**Validation:**
- Compiles without errors
- All properties are init-only (immutable)
- OverallPercentComplete calculation is correct

---

#### 2. `src/DotNetUp.Core/Models/InstallationContext.cs` 🔄 UPDATE

**Purpose:** Add progress reporting capabilities

**Changes Required:**

1. **Change Progress property type:**
```csharp
// OLD:
public IProgress<string>? Progress { get; init; }

// NEW:
public IProgress<InstallationProgress>? Progress { get; init; }
```

2. **Remove old method:**
```csharp
// REMOVE THIS:
public void ReportProgress(string message)
{
    Progress?.Report(message);
    Logger.LogInformation("Progress: {Message}", message);
}
```

3. **Add internal state fields:**
```csharp
// Add these private fields:
private int _currentStepNumber;
private int _totalSteps;
private string _currentStepName = string.Empty;
```

4. **Add new methods:**
```csharp
/// <summary>
/// Sets the current step context. Called by the executor before executing each step.
/// </summary>
/// <param name="stepNumber">Current step number (1-based)</param>
/// <param name="totalSteps">Total number of steps</param>
/// <param name="stepName">Name of the current step</param>
internal void SetCurrentStep(int stepNumber, int totalSteps, string stepName)
{
    _currentStepNumber = stepNumber;
    _totalSteps = totalSteps;
    _currentStepName = stepName;
}

/// <summary>
/// Reports progress for the current step. Called by the step implementation.
/// </summary>
/// <param name="subStepDescription">Description of the current substep</param>
/// <param name="percentComplete">Completion percentage (0-100)</param>
public void ReportStepProgress(string subStepDescription, int percentComplete)
{
    if (percentComplete < 0 || percentComplete > 100)
        throw new ArgumentOutOfRangeException(nameof(percentComplete),
            "Percent complete must be between 0 and 100");

    var progress = new InstallationProgress
    {
        CurrentStepNumber = _currentStepNumber,
        TotalSteps = _totalSteps,
        CurrentStepName = _currentStepName,
        SubStepDescription = subStepDescription,
        PercentComplete = percentComplete
    };

    Progress?.Report(progress);
    Logger.LogInformation(
        "Step {StepNumber}/{TotalSteps} ({StepName}): {SubStep} - {Percent}% ({Overall:F1}% overall)",
        progress.CurrentStepNumber,
        progress.TotalSteps,
        progress.CurrentStepName,
        subStepDescription,
        percentComplete,
        progress.OverallPercentComplete);
}
```

**Validation:**
- Compiles without errors
- Validation throws ArgumentOutOfRangeException for invalid percentage
- Logging includes all relevant fields

---

#### 3. `src/DotNetUp.Core/Builders/InstallationBuilder.cs` 🔄 UPDATE

**Purpose:** Update builder to use new progress type

**Changes Required:**

1. **Update field declaration:**
```csharp
// OLD:
private IProgress<string>? _progress;

// NEW:
private IProgress<InstallationProgress>? _progress;
```

2. **Update method signature:**
```csharp
// OLD:
public InstallationBuilder WithProgress(IProgress<string> progress)

// NEW:
public InstallationBuilder WithProgress(IProgress<InstallationProgress> progress)
{
    _progress = progress;
    return this;
}
```

**Validation:**
- Compiles without errors
- Fluent API still works

---

**🔹 PHASE 1 CHECKPOINT:**
```bash
git add src/DotNetUp.Core/Models/InstallationProgress.cs
git add src/DotNetUp.Core/Models/InstallationContext.cs
git add src/DotNetUp.Core/Builders/InstallationBuilder.cs
git commit -m "Add InstallationProgress model and update context for structured progress reporting"
git push -u origin claude/review-and-plan-k14z5
```

**Validation:**
- ✅ Build passes on Raspberry Pi
- ✅ No compilation errors
- **DO NOT PROCEED** if build fails

---

### 📋 PHASE 2: Test Infrastructure

#### 4. `tests/DotNetUp.Tests/DotNetUp.Tests.csproj` 🔄 UPDATE

**Purpose:** Add testing dependencies

**Changes Required:**

Add inside `<ItemGroup>` with other PackageReferences:
```xml
<PackageReference Include="NSubstitute" Version="5.3.0" />
<PackageReference Include="FluentAssertions" Version="6.12.0" />
```

**Validation:**
- Package restore succeeds
- Build succeeds with new packages

---

#### 5. `tests/DotNetUp.Tests/README.md` ✨ NEW

**Purpose:** Document testing approach and toolset

**Contents:** (Full file - see detailed content below)

Key sections:
- Testing Framework & Tools (xUnit, NSubstitute, FluentAssertions)
- Project Structure
- Testing Patterns (AAA pattern)
- Test Naming Convention
- Running Tests
- Test Coverage Goals
- Best Practices

**Validation:**
- README is comprehensive and clear
- Examples are accurate

---

#### 6. `CLAUDE.md` 🔄 UPDATE

**Purpose:** Reference the testing documentation

**Changes Required:**

Update the **Testing** section (around line 150):

```markdown
## Testing

- Use **xUnit** framework
- Use **NSubstitute** for mocking
- Use **FluentAssertions** for readable assertions
- Create **TestInstallationContext** fixture
- Mock **file system, registry, database** etc.
- Test **success path, failure path, rollback path**
- Test **edge cases** (already exists, missing prerequisites)

**See detailed testing guidelines:** `tests/DotNetUp.Tests/README.md`
```

**Validation:**
- Link to testing README is visible
- Testing section updated with correct tools

---

#### 7. `tests/DotNetUp.Tests/Fixtures/TestInstallationContext.cs` ✨ NEW

**Purpose:** Helper factory for creating test contexts

**Contents:**
```csharp
using DotNetUp.Core.Models;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace DotNetUp.Tests.Fixtures;

/// <summary>
/// Factory for creating test installation contexts with sensible defaults.
/// </summary>
public static class TestInstallationContext
{
    /// <summary>
    /// Creates a test installation context with optional customization.
    /// </summary>
    public static InstallationContext Create(
        ILogger? logger = null,
        IProgress<InstallationProgress>? progress = null,
        Dictionary<string, object>? properties = null,
        CancellationToken cancellationToken = default)
    {
        var testLogger = logger ?? Substitute.For<ILogger>();
        var context = new InstallationContext(testLogger, progress, cancellationToken);

        if (properties != null)
        {
            foreach (var kvp in properties)
                context.Properties[kvp.Key] = kvp.Value;
        }

        return context;
    }
}
```

**Validation:**
- Compiles without errors
- Can be used in tests

---

#### 8. `tests/DotNetUp.Tests/Fixtures/MockInstallationStep.cs` ✨ NEW

**Purpose:** Configurable mock step for testing executor

**Contents:**
```csharp
using DotNetUp.Core.Interfaces;
using DotNetUp.Core.Models;

namespace DotNetUp.Tests.Fixtures;

/// <summary>
/// Mock installation step for testing. Configurable to simulate success/failure.
/// </summary>
public class MockInstallationStep : IInstallationStep
{
    public string Name { get; set; } = "MockStep";
    public string Description { get; set; } = "Mock step for testing";

    public bool ValidateShouldSucceed { get; set; } = true;
    public bool ExecuteShouldSucceed { get; set; } = true;
    public bool RollbackShouldSucceed { get; set; } = true;

    public TimeSpan ExecutionDelay { get; set; } = TimeSpan.Zero;
    public List<string> CallHistory { get; } = new();

    public bool ValidateCalled { get; private set; }
    public bool ExecuteCalled { get; private set; }
    public bool RollbackCalled { get; private set; }

    public Task<InstallationResult> ValidateAsync(InstallationContext context)
    {
        ValidateCalled = true;
        CallHistory.Add("Validate");

        return Task.FromResult(ValidateShouldSucceed
            ? InstallationResult.SuccessResult($"{Name} validation succeeded")
            : InstallationResult.FailureResult($"{Name} validation failed"));
    }

    public async Task<InstallationResult> ExecuteAsync(InstallationContext context)
    {
        ExecuteCalled = true;
        CallHistory.Add("Execute");

        if (ExecutionDelay > TimeSpan.Zero)
            await Task.Delay(ExecutionDelay, context.CancellationToken);

        return ExecuteShouldSucceed
            ? InstallationResult.SuccessResult($"{Name} executed")
            : InstallationResult.FailureResult($"{Name} execution failed");
    }

    public Task<InstallationResult> RollbackAsync(InstallationContext context)
    {
        RollbackCalled = true;
        CallHistory.Add("Rollback");

        return Task.FromResult(RollbackShouldSucceed
            ? InstallationResult.SuccessResult($"{Name} rolled back")
            : InstallationResult.FailureResult($"{Name} rollback failed"));
    }
}
```

**Validation:**
- Compiles without errors
- Can simulate all scenarios

---

**🔹 PHASE 2 CHECKPOINT:**
```bash
git add tests/DotNetUp.Tests/DotNetUp.Tests.csproj
git add tests/DotNetUp.Tests/README.md
git add CLAUDE.md
git add tests/DotNetUp.Tests/Fixtures/TestInstallationContext.cs
git add tests/DotNetUp.Tests/Fixtures/MockInstallationStep.cs
git commit -m "Add test infrastructure, fixtures, and testing documentation"
git push
```

**Validation:**
- ✅ Build passes
- ✅ All new files compile
- **DO NOT PROCEED** if build fails

---

### 📋 PHASE 3: Model Tests (Build Confidence)

#### 9. `tests/DotNetUp.Tests/Models/InstallationProgressTests.cs` ✨ NEW

**Purpose:** Test the new progress model

**Test Cases:**
```csharp
[Fact]
public void Constructor_SetsAllProperties_Correctly()

[Theory]
[InlineData(1, 3, 50, 16.67)]   // Step 1/3 at 50% = 16.67% overall
[InlineData(2, 3, 0, 33.33)]     // Step 2/3 at 0% = 33.33% overall
[InlineData(3, 3, 100, 100.0)]   // Step 3/3 at 100% = 100% overall
[InlineData(1, 1, 50, 50.0)]     // Single step at 50% = 50% overall
public void OverallPercentComplete_CalculatesCorrectly(int step, int total, int percent, double expected)

[Fact]
public void OverallPercentComplete_WithZeroSteps_ReturnsZero()
```

**Pattern:** xUnit + FluentAssertions

**Validation:**
- All tests pass
- Calculation logic verified

---

#### 10. `tests/DotNetUp.Tests/Models/InstallationContextTests.cs` ✨ NEW

**Purpose:** Test context functionality

**Test Cases:**
```csharp
[Fact]
public void Constructor_WithNullLogger_ThrowsArgumentNullException()

[Fact]
public void Properties_CanBeAddedAndRetrieved()

[Fact]
public void SetCurrentStep_UpdatesInternalState()

[Fact]
public void ReportStepProgress_CreatesCorrectInstallationProgress()

[Theory]
[InlineData(-1)]
[InlineData(101)]
public void ReportStepProgress_WithInvalidPercentage_ThrowsArgumentOutOfRangeException(int invalidPercent)

[Fact]
public void ReportStepProgress_CallsProgressReporter()
// Uses NSubstitute to capture progress reports

[Fact]
public void ReportStepProgress_LogsStructuredInformation()
// Uses NSubstitute to verify logging

[Fact]
public void ReportStepProgress_WithNullProgress_DoesNotThrow()
```

**Pattern:** xUnit + NSubstitute + FluentAssertions

**Validation:**
- All tests pass
- Progress reporting verified
- Logging verified

---

#### 11. `tests/DotNetUp.Tests/Models/InstallationResultTests.cs` ✨ NEW

**Purpose:** Test result model

**Test Cases:**
```csharp
[Fact]
public void SuccessResult_CreatesSuccessfulResult()

[Fact]
public void FailureResult_CreatesFailedResult()

[Fact]
public void SuccessResult_WithData_InitializesDataDictionary()

[Fact]
public void FailureResult_WithException_StoresException()

[Fact]
public void SuccessResult_WithNullData_CreatesEmptyDictionary()

[Fact]
public void FailureResult_WithNullData_CreatesEmptyDictionary()
```

**Pattern:** xUnit + FluentAssertions

**Validation:**
- All tests pass
- Factory methods verified

---

**🔹 PHASE 3 CHECKPOINT:**
```bash
git add tests/DotNetUp.Tests/Models/InstallationProgressTests.cs
git add tests/DotNetUp.Tests/Models/InstallationContextTests.cs
git add tests/DotNetUp.Tests/Models/InstallationResultTests.cs
git commit -m "Add model tests for InstallationProgress, InstallationContext, and InstallationResult"
git push
```

**Validation:**
- ✅ All model tests pass on Raspberry Pi
- ✅ No test failures
- **DO NOT PROCEED** if tests fail

---

### 📋 PHASE 4: Executor Tests (Test-First - TDD)

#### 12. `tests/DotNetUp.Tests/Execution/InstallationTests.cs` ✨ NEW

**Purpose:** Comprehensive tests for executor (WRITTEN BEFORE IMPLEMENTATION)

**Test Categories:**

**Constructor Tests:**
```csharp
[Fact]
public void Constructor_WithNullSteps_ThrowsArgumentNullException()

[Fact]
public void Constructor_WithNullContext_ThrowsArgumentNullException()

[Fact]
public void Constructor_WithEmptySteps_ThrowsArgumentException()
```

**Validation Phase Tests:**
```csharp
[Fact]
public async Task ExecuteAsync_ValidatesAllSteps_BeforeExecution()

[Fact]
public async Task ExecuteAsync_WhenValidationFails_ReturnsFailureImmediately()

[Fact]
public async Task ExecuteAsync_WhenValidationFails_DoesNotExecuteAnySteps()
```

**Execution Phase Tests:**
```csharp
[Fact]
public async Task ExecuteAsync_ExecutesSteps_InOrder()

[Fact]
public async Task ExecuteAsync_CallsSetCurrentStep_BeforeEachExecution()

[Fact]
public async Task ExecuteAsync_WhenAllStepsSucceed_ReturnsSuccess()

[Fact]
public async Task ExecuteAsync_WhenStepFails_ReturnsFailure()

[Fact]
public async Task ExecuteAsync_WhenStepFails_DoesNotExecuteRemainingSteps()
```

**Rollback Tests:**
```csharp
[Fact]
public async Task ExecuteAsync_WhenStepFails_TriggersRollback()

[Fact]
public async Task ExecuteAsync_RollsBackSteps_InReverseOrder()

[Fact]
public async Task ExecuteAsync_RollsBackOnlyExecutedSteps_NotValidatedOnlySteps()

[Fact]
public async Task ExecuteAsync_WhenRollbackFails_ContinuesRollingBackOtherSteps()

[Fact]
public async Task ExecuteAsync_WhenMultipleRollbacksFail_LogsAllFailures()
```

**Cancellation Tests:**
```csharp
[Fact]
public async Task ExecuteAsync_WhenCancelled_ThrowsOperationCanceledException()

[Fact]
public async Task ExecuteAsync_WhenCancelled_TriggersRollback()
```

**Progress Tests:**
```csharp
[Fact]
public async Task ExecuteAsync_ReportsProgressForEachStep()

[Fact]
public async Task ExecuteAsync_ReportsCorrectStepNumbers()
```

**Integration Tests:**
```csharp
[Fact]
public async Task ExecuteAsync_ComplexScenario_Step3Fails_RollsBackSteps1And2()

[Fact]
public async Task ExecuteAsync_EndToEnd_AllStepsSucceed()
```

**Pattern:** xUnit + NSubstitute + FluentAssertions + MockInstallationStep

**Validation:**
- ✅ All ~20 test cases written
- ✅ Tests compile but fail (no implementation yet - THIS IS EXPECTED)
- ✅ Tests use MockInstallationStep fixture
- ✅ Tests use FluentAssertions for readability
- ✅ Tests use NSubstitute for mocking

---

**🔹 PHASE 4 CHECKPOINT:**
```bash
git add tests/DotNetUp.Tests/Execution/InstallationTests.cs
git commit -m "Add comprehensive executor tests (TDD - tests written first)"
git push
```

**Validation:**
- ✅ Tests compile
- ❌ Tests fail (expected - no implementation yet)
- **DO NOT PROCEED** if tests don't compile

---

### 📋 PHASE 5: Executor Implementation

#### 13. `src/DotNetUp.Core/Execution/Installation.cs` ✨ NEW

**Purpose:** Core executor implementation (MAKE TESTS PASS)

**Class Structure:**
```csharp
namespace DotNetUp.Core.Execution;

public class Installation
{
    private readonly IReadOnlyList<IInstallationStep> _steps;
    private readonly InstallationContext _context;

    public Installation(IReadOnlyList<IInstallationStep> steps, InstallationContext context)
    {
        _steps = steps ?? throw new ArgumentNullException(nameof(steps));
        _context = context ?? throw new ArgumentNullException(nameof(context));

        if (steps.Count == 0)
            throw new ArgumentException("At least one step is required", nameof(steps));
    }

    public async Task<InstallationResult> ExecuteAsync()
    {
        // Implementation to make all tests pass
    }

    private async Task<InstallationResult> ValidateAllStepsAsync()
    {
        // Validate all steps upfront
    }

    private async Task RollbackAsync(List<IInstallationStep> executedSteps)
    {
        // Best-effort rollback in reverse order
    }
}
```

**Implementation Algorithm:**

1. **ExecuteAsync():**
   - Validate all steps (fail fast)
   - For each step:
     - Check CancellationToken
     - Call SetCurrentStep on context
     - Log execution
     - Execute step
     - Track executed steps
     - If failure: trigger rollback and return failure
   - Return overall success

2. **ValidateAllStepsAsync():**
   - Log validation start
   - For each step:
     - Call ValidateAsync
     - If any fails: return failure immediately
   - Return success

3. **RollbackAsync():**
   - Log rollback start
   - Reverse executed steps list
   - For each step in reverse:
     - Try to call RollbackAsync
     - If fails: log warning but continue (best-effort)
     - Catch exceptions: log error but continue

**Key Principles:**
- Best-effort rollback (failures logged, not thrown)
- Check CancellationToken between steps
- Structured logging throughout
- Return InstallationResult (no exceptions)

**Validation:**
- ✅ All executor tests pass
- ✅ Code follows algorithm
- ✅ Best-effort rollback implemented
- ✅ Proper logging throughout

---

**🔹 PHASE 5 CHECKPOINT:**
```bash
git add src/DotNetUp.Core/Execution/Installation.cs
git commit -m "Implement Installation executor with validation, execution, and rollback"
git push
```

**Validation:**
- ✅ All tests pass on Raspberry Pi
- ✅ No compilation errors
- **DO NOT PROCEED** if tests fail

---

### 📋 PHASE 6: Builder Integration Tests

#### 14. `tests/DotNetUp.Tests/Builders/InstallationBuilderTests.cs` ✨ NEW

**Purpose:** Test builder and validate integration

**Test Cases:**
```csharp
[Fact]
public void WithStep_AddsStepToBuilder()

[Fact]
public void WithStep_WithNullStep_ThrowsArgumentNullException()

[Fact]
public void WithProperty_AddsPropertyToBuilder()

[Fact]
public void WithProperty_WithNullOrEmptyKey_ThrowsArgumentException()

[Fact]
public void WithLogger_SetsLogger()

[Fact]
public void WithLogger_WithNullLogger_ThrowsArgumentNullException()

[Fact]
public void WithProgress_SetsProgressReporter()

[Fact]
public void WithCancellationToken_SetsToken()

[Fact]
public void Build_WithoutLogger_ThrowsInvalidOperationException()

[Fact]
public void Build_WithoutSteps_ThrowsInvalidOperationException()

[Fact]
public void Build_ReturnsCorrectTuple()

[Fact]
public void Build_CopiesPropertiesToContext()

[Fact]
public async Task Build_EndToEnd_BuilderToExecutorIntegration()
// Complete flow: Builder -> Build -> Installation -> ExecuteAsync
```

**Pattern:** xUnit + NSubstitute + FluentAssertions

**Validation:**
- All tests pass
- End-to-end integration verified

---

**🔹 PHASE 6 CHECKPOINT:**
```bash
git add tests/DotNetUp.Tests/Builders/InstallationBuilderTests.cs
git commit -m "Add builder tests and validate end-to-end integration"
git push
```

**Validation:**
- ✅ All builder tests pass
- ✅ Integration verified
- **DO NOT PROCEED** if tests fail

---

### 📋 PHASE 7: Integration Examples

#### 15. `tests/DotNetUp.Tests/Integration/EndToEndTests.cs` ✨ NEW

**Purpose:** Realistic end-to-end scenarios

**Test Cases:**
```csharp
[Fact]
public async Task CompleteInstallation_WithMultipleSteps_SucceedsAndReportsProgress()

[Fact]
public async Task FailedInstallation_TriggersRollback_AndReportsCorrectly()

[Fact]
public async Task CancelledInstallation_StopsExecution_AndRollsBack()

[Fact]
public async Task ProgressReporting_CapturesAllStepTransitions()
// Verifies SetCurrentStep called with correct parameters for each step
```

**Pattern:** xUnit + NSubstitute + FluentAssertions + MockInstallationStep

**Validation:**
- All integration tests pass
- Progress events captured and validated
- Logging verified

---

**🔹 PHASE 7 CHECKPOINT:**
```bash
git add tests/DotNetUp.Tests/Integration/EndToEndTests.cs
git commit -m "Add end-to-end integration tests"
git push
```

**Validation:**
- ✅ All integration tests pass
- ✅ Test coverage ≥ 80%
- ✅ Green build

---

## Success Criteria

### Deliverables Checklist:
- [ ] All 15 files created/updated
- [ ] All tests passing on Raspberry Pi
- [ ] Build is green (no errors or warnings)
- [ ] Test coverage ≥ 80% for executor
- [ ] Testing documentation in place
- [ ] Structured progress reporting works end-to-end
- [ ] Best-effort rollback implemented and tested
- [ ] CLAUDE.md references testing docs
- [ ] Ready for FileSystem steps implementation (next iteration)

### Phase 1 MVP Progress:
**Before:** 40% Complete
**After:** 75% Complete

### What's Complete:
✅ Core interfaces (IInstallationStep)
✅ Models (InstallationResult, InstallationContext, InstallationProgress)
✅ Builder (InstallationBuilder with fluent API)
✅ **Executor (Installation with validation, execution, rollback)**
✅ **Structured progress reporting system**
✅ **Comprehensive test suite**

### What's Next (Future Iteration):
❌ FileSystem steps (CopyFile, MoveFile, DeleteFile)
❌ README.md with usage examples
❌ Real file system operations
❌ Additional step types (Registry, Services, etc.)

---

## Out of Scope

**NOT in this iteration:**
- FileSystem step implementations
- End-user documentation (README.md)
- Performance optimization
- Real file operations
- Other step types (Registry, Services, Database, IIS)

---

## Testing Tools Reference

**xUnit:** Test framework (already in project)
- `[Fact]` - Simple tests
- `[Theory]` with `[InlineData]` - Parameterized tests

**NSubstitute:** Mocking framework
- `Substitute.For<T>()` - Create mocks
- `.Returns()` - Setup return values
- `.Received()` - Verify calls

**FluentAssertions:** Assertion library
- `.Should().Be()`, `.Should().BeTrue()`, etc.
- Readable, expressive assertions

**See:** `tests/DotNetUp.Tests/README.md` for complete guide

---

## Build Validation Workflow

After each phase checkpoint:

1. **Commit and push changes**
2. **GitHub Actions automatically builds on Raspberry Pi**
3. **Check build status:**
   ```bash
   curl -H "Authorization: token $GITHUB_TOKEN" \
     "https://api.github.com/repos/SzymonWelter/dotNETup/actions/runs?branch=claude/review-and-plan-k14z5&per_page=1"
   ```
4. **If green:** Continue to next phase
5. **If red:** Fix immediately, don't proceed

---

## Key Design Decisions

**Progress Reporting:**
- Executor manages: step number, total steps, step name
- Step manages: substep description, completion percentage
- Separation via `internal SetCurrentStep()` and `public ReportStepProgress()`

**Rollback:**
- Best-effort: failures logged but don't stop rollback
- Reverse order (LIFO): last executed rolls back first
- Only executed steps rolled back (not validated-only)

**Testing:**
- Test-first approach (TDD)
- MockInstallationStep for executor testing
- NSubstitute for interface mocking
- FluentAssertions for readability

**Error Handling:**
- Validate all steps before execution (fail fast)
- Return InstallationResult (no exceptions from executor)
- Log structured information throughout

---

**Plan Version:** 1.0
**Created:** December 15, 2025
**Status:** Ready for Implementation
**Next Review:** After Phase 7 completion
