# DotNetUp Testing Guide

This document describes the testing approach, tools, and patterns used in the DotNetUp project.

---

## Testing Framework & Tools

### xUnit (Test Framework)
- **Purpose:** Test runner and organization
- **Usage:**
  - `[Fact]` - Simple test cases
  - `[Theory]` with `[InlineData]` - Parameterized tests (same test, multiple inputs)
- **Documentation:** https://xunit.net/

**Example:**
```csharp
[Fact]
public void Constructor_WithNullLogger_ThrowsArgumentNullException()
{
    // Test implementation
}

[Theory]
[InlineData(1, 3, 50, 16.67)]
[InlineData(2, 3, 0, 33.33)]
public void OverallPercentComplete_CalculatesCorrectly(int step, int total, int percent, double expected)
{
    // Test implementation uses parameters
}
```

---

### NSubstitute (Mocking Framework)
- **Purpose:** Create mock objects and verify interactions
- **Key Features:**
  - Simple, readable syntax
  - Verify method calls
  - Configure return values
- **Documentation:** https://nsubstitute.github.io/

**Example:**
```csharp
// Create a mock
var mockLogger = Substitute.For<ILogger>();

// Configure return value
mockStep.ValidateAsync(Arg.Any<InstallationContext>())
    .Returns(InstallationResult.SuccessResult("OK"));

// Verify a call was made
mockLogger.Received(1).LogInformation(Arg.Any<string>(), Arg.Any<object[]>());

// Verify a call was NOT made
mockStep.DidNotReceive().RollbackAsync(Arg.Any<InstallationContext>());
```

---

### FluentAssertions (Assertion Library)
- **Purpose:** Readable, expressive assertions
- **Key Features:**
  - Natural language syntax
  - Detailed failure messages
  - Chainable assertions
- **Documentation:** https://fluentassertions.com/

**Example:**
```csharp
// Instead of: Assert.True(result.Success)
result.Success.Should().BeTrue();

// Instead of: Assert.Equal("expected", result.Message)
result.Message.Should().Be("expected");

// Collection assertions
list.Should().HaveCount(3);
list.Should().Contain(x => x.Name == "Step1");

// Exception assertions
Action act = () => builder.Build();
act.Should().Throw<InvalidOperationException>()
   .WithMessage("At least one step is required");
```

---

## Project Structure

```
tests/DotNetUp.Tests/
├── Fixtures/              # Test helpers and utilities
│   ├── TestInstallationContext.cs    # Factory for test contexts
│   └── MockInstallationStep.cs       # Configurable mock step
├── Models/                # Tests for model classes
│   ├── InstallationProgressTests.cs
│   ├── InstallationContextTests.cs
│   └── InstallationResultTests.cs
├── Builders/              # Tests for builder classes
│   └── InstallationBuilderTests.cs
├── Execution/             # Tests for executor
│   └── InstallationTests.cs
├── Integration/           # End-to-end integration tests
│   └── EndToEndTests.cs
└── README.md              # This file
```

---

## Testing Patterns

### AAA Pattern (Arrange-Act-Assert)
All tests follow this structure:

```csharp
[Fact]
public void MethodName_Scenario_ExpectedBehavior()
{
    // Arrange - Set up test data and dependencies
    var context = TestInstallationContext.Create();
    var step = new MockInstallationStep { Name = "TestStep" };

    // Act - Execute the code under test
    var result = await step.ValidateAsync(context);

    // Assert - Verify the outcome
    result.Success.Should().BeTrue();
}
```

---

## Test Naming Convention

Format: `MethodName_Scenario_ExpectedBehavior`

**Examples:**
- `Constructor_WithNullLogger_ThrowsArgumentNullException`
- `ExecuteAsync_WhenAllStepsSucceed_ReturnsSuccess`
- `RollbackAsync_WhenStepFails_ContinuesRollingBackOtherSteps`
- `OverallPercentComplete_CalculatesCorrectly`

**Guidelines:**
- Be descriptive and specific
- Clearly state what's being tested
- Indicate the expected outcome
- Don't worry about long names

---

## Running Tests

### Using .NET CLI
```bash
# Run all tests
dotnet test

# Run tests in a specific file
dotnet test --filter "FullyQualifiedName~InstallationProgressTests"

# Run a specific test
dotnet test --filter "FullyQualifiedName~InstallationProgressTests.Constructor_SetsAllProperties_Correctly"

# Run tests with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run tests and generate coverage report
dotnet test /p:CollectCoverage=true
```

### Using Visual Studio
- Test Explorer (Ctrl+E, T)
- Run All / Run Selected
- Debug Tests

### Using Rider
- Unit Tests window
- Run / Debug individual or all tests
- View coverage inline

---

## Test Coverage Goals

**Minimum Coverage:** 80% overall

**Target Coverage by Component:**
- **Models:** 90%+ (simple, easy to test)
- **Builders:** 85%+ (fluent API validation)
- **Executor:** 80%+ (complex logic, many paths)
- **Steps:** 80%+ (validate, execute, rollback paths)

**What to Cover:**
- ✅ Success paths (happy path)
- ✅ Failure paths (error conditions)
- ✅ Edge cases (null, empty, boundary values)
- ✅ Validation logic
- ✅ Rollback behavior
- ✅ Cancellation handling

**What NOT to Cover:**
- ❌ Auto-generated code
- ❌ Simple property getters/setters
- ❌ Framework code

---

## Best Practices

### 1. Test One Thing at a Time
Each test should verify a single behavior.

**Good:**
```csharp
[Fact]
public void ExecuteAsync_WhenStepFails_ReturnsFailure()

[Fact]
public void ExecuteAsync_WhenStepFails_DoesNotExecuteRemainingSteps()
```

**Bad:**
```csharp
[Fact]
public void ExecuteAsync_WhenStepFails_ReturnsFailureAndDoesNotExecuteRemainingSteps()
```

---

### 2. Use Test Fixtures for Reusable Setup
Common setup code goes in fixtures.

**Example:**
```csharp
// Instead of repeating this in every test:
var logger = Substitute.For<ILogger>();
var context = new InstallationContext(logger);

// Use the fixture:
var context = TestInstallationContext.Create();
```

---

### 3. Make Tests Readable
Tests are documentation. Make them clear.

**Good:**
```csharp
var step = new MockInstallationStep
{
    Name = "DatabaseMigration",
    ExecuteShouldSucceed = false
};
```

**Bad:**
```csharp
var step = new MockInstallationStep { Name = "S1", ExecuteShouldSucceed = false };
```

---

### 4. Test Behavior, Not Implementation
Focus on what the code does, not how it does it.

**Good:**
```csharp
// Tests that rollback happens in reverse order
result.Should().Be("Step3,Step2,Step1");
```

**Bad:**
```csharp
// Tests internal implementation details
_steps.Reverse().Should().HaveCount(3);
```

---

### 5. Use Descriptive Assertion Messages
FluentAssertions provides good messages by default, but you can add context.

**Example:**
```csharp
result.Success.Should().BeTrue("because all validation steps passed");
progress.OverallPercentComplete.Should().BeApproximately(expected, 0.01,
    "because step {0} of {1} at {2}% should equal {3}%",
    step, total, percent, expected);
```

---

### 6. Avoid Test Interdependencies
Each test should run independently.

**Good:**
- Tests create their own data
- Tests don't rely on execution order
- Tests clean up after themselves

**Bad:**
- Tests share mutable state
- Tests depend on previous test results
- Tests require specific execution order

---

### 7. Use Theory for Parameterized Tests
When testing the same logic with different inputs, use `[Theory]`.

**Example:**
```csharp
[Theory]
[InlineData(0, 0)]
[InlineData(50, 50)]
[InlineData(100, 100)]
public void PercentComplete_IsSetCorrectly(int input, int expected)
{
    var progress = new InstallationProgress { PercentComplete = input };
    progress.PercentComplete.Should().Be(expected);
}
```

---

## Mocking Guidelines

### When to Mock
- External dependencies (file system, database, network)
- Interfaces from other components
- Services you don't control

### When NOT to Mock
- Value objects (models, DTOs)
- Your own concrete classes (test them directly)
- Simple dependencies

### Mock Configuration Examples

**Return Values:**
```csharp
mockStep.ValidateAsync(Arg.Any<InstallationContext>())
    .Returns(InstallationResult.SuccessResult("OK"));
```

**Multiple Calls, Different Returns:**
```csharp
mockStep.ValidateAsync(Arg.Any<InstallationContext>())
    .Returns(
        InstallationResult.SuccessResult("First call"),
        InstallationResult.FailureResult("Second call")
    );
```

**Throw Exceptions:**
```csharp
mockStep.ExecuteAsync(Arg.Any<InstallationContext>())
    .Throws(new InvalidOperationException("Something broke"));
```

**Argument Matching:**
```csharp
// Any argument
mockLogger.LogInformation(Arg.Any<string>());

// Specific value
mockLogger.LogInformation("Starting installation");

// Custom predicate
mockStep.ValidateAsync(Arg.Is<InstallationContext>(c => c.Properties.Count > 0));
```

---

## Common Testing Scenarios

### Testing Exceptions
```csharp
[Fact]
public void Constructor_WithNullLogger_ThrowsArgumentNullException()
{
    Action act = () => new InstallationContext(null!);

    act.Should().Throw<ArgumentNullException>()
       .WithParameterName("logger");
}
```

### Testing Async Methods
```csharp
[Fact]
public async Task ExecuteAsync_WhenStepSucceeds_ReturnsSuccess()
{
    var step = new MockInstallationStep { ExecuteShouldSucceed = true };
    var context = TestInstallationContext.Create();

    var result = await step.ExecuteAsync(context);

    result.Success.Should().BeTrue();
}
```

### Testing Progress Reporting
```csharp
[Fact]
public void ReportStepProgress_CallsProgressReporter()
{
    var progressReports = new List<InstallationProgress>();
    var progress = new Progress<InstallationProgress>(p => progressReports.Add(p));
    var context = TestInstallationContext.Create(progress: progress);

    context.SetCurrentStep(1, 3, "Step1");
    context.ReportStepProgress("Doing work", 50);

    progressReports.Should().HaveCount(1);
    progressReports[0].CurrentStepNumber.Should().Be(1);
    progressReports[0].PercentComplete.Should().Be(50);
}
```

### Testing Logging
```csharp
[Fact]
public void ReportStepProgress_LogsStructuredInformation()
{
    var mockLogger = Substitute.For<ILogger>();
    var context = TestInstallationContext.Create(logger: mockLogger);

    context.SetCurrentStep(2, 5, "TestStep");
    context.ReportStepProgress("Working", 75);

    mockLogger.Received(1).LogInformation(
        Arg.Is<string>(s => s.Contains("Step")),
        Arg.Any<object[]>());
}
```

### Testing Cancellation
```csharp
[Fact]
public async Task ExecuteAsync_WhenCancelled_ThrowsOperationCanceledException()
{
    var cts = new CancellationTokenSource();
    cts.Cancel();
    var context = TestInstallationContext.Create(cancellationToken: cts.Token);

    var installation = new Installation(steps, context);

    Func<Task> act = async () => await installation.ExecuteAsync();
    await act.Should().ThrowAsync<OperationCanceledException>();
}
```

---

## Test-Driven Development (TDD) Approach

This project uses TDD for complex components like the executor.

**TDD Workflow:**
1. ✍️ Write a failing test (RED)
2. ✅ Write minimal code to make it pass (GREEN)
3. 🔄 Refactor while keeping tests green (REFACTOR)

**Benefits:**
- Ensures testability from the start
- Provides clear requirements
- Catches regressions immediately
- Improves design

**Example:**
```
1. Write: ExecuteAsync_ValidatesAllSteps_BeforeExecution (fails)
2. Implement: ValidateAllStepsAsync() method (passes)
3. Refactor: Extract validation logic, improve names (still passes)
```

---

## Continuous Integration

Tests run automatically on every push via GitHub Actions.

**Build Pipeline:**
1. Restore packages
2. Build solution
3. Run all tests
4. Generate coverage report
5. Fail build if tests fail

**Local Validation:**
Before pushing, always run:
```bash
dotnet build
dotnet test
```

---

## Troubleshooting

### Tests Won't Run
- Ensure packages are restored: `dotnet restore`
- Check test project references the main project
- Verify test discovery (check Test Explorer)

### Mocks Not Working
- Ensure you're mocking interfaces, not concrete classes
- Check that the method is virtual (if mocking concrete class)
- Verify argument matchers are correct

### Async Tests Hanging
- Always use `await` with async methods
- Check for deadlocks (don't use `.Result` or `.Wait()`)
- Set a timeout: `[Fact(Timeout = 5000)]`

### Coverage Not Generated
- Install coverage tool: `dotnet add package coverlet.collector`
- Run with coverage: `dotnet test /p:CollectCoverage=true`

---

## Resources

- **xUnit:** https://xunit.net/
- **NSubstitute:** https://nsubstitute.github.io/
- **FluentAssertions:** https://fluentassertions.com/
- **Testing Best Practices:** https://learn.microsoft.com/en-us/dotnet/core/testing/

---

**Last Updated:** December 2025
**Maintained By:** DotNetUp Team
