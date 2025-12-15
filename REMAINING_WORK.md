# Remaining Work - Installation Orchestration Library

## ✅ Completed (This Session)

### Infrastructure Setup
- ✅ Raspberry Pi 4 self-hosted GitHub Actions runner configured
- ✅ GitHub Actions workflow for build/test validation
- ✅ Workflow triggers on all `claude/**` branches automatically
- ✅ GitHub API integration for checking build status
- ✅ Updated CLAUDE.md with build validation workflow for Claude Code web

### Phase 1 MVP - Foundation (Partial)
- ✅ **IInstallationStep** interface - Core abstraction with Validate/Execute/Rollback pattern
- ✅ **InstallationResult** model - Standard response with Success/Message/Exception/Data
- ✅ **InstallationContext** model - Shared state with Logger/Progress/Properties/CancellationToken
- ✅ **InstallationBuilder** - Fluent API for configuring installations
- ✅ **First successful build** - All code compiles on Raspberry Pi runner

**Project Structure:**
```
src/DotNetUp.Core/
  ├── Interfaces/
  │   └── IInstallationStep.cs
  ├── Models/
  │   ├── InstallationContext.cs
  │   └── InstallationResult.cs
  └── Builders/
      └── InstallationBuilder.cs
```

---

## 🚧 Remaining Work for Phase 1 MVP

### 1. Installation Executor (High Priority)

**File:** `src/DotNetUp.Core/Execution/Installation.cs`

**Purpose:** Orchestrates the execution of installation steps with validation and rollback.

**Key Features:**
- Takes steps and context from InstallationBuilder.Build()
- Validates all steps before execution (fail fast)
- Executes steps sequentially
- On failure: Rolls back completed steps in reverse order
- Returns overall InstallationResult summary

**Algorithm:**
```csharp
1. Validate all steps (call ValidateAsync on each)
2. If any validation fails → return failure result immediately
3. Execute each step (call ExecuteAsync)
4. Track executed steps for rollback
5. If any step fails:
   - Log the failure
   - Roll back all executed steps in reverse order
   - Return failure result with rollback summary
6. If all steps succeed → return success result
```

**Key Design Decisions:**
- Best-effort rollback: If rollback fails, log it but continue rolling back other steps
- Don't throw exceptions - return InstallationResult
- Check CancellationToken between steps

---

### 2. FileSystem Step Implementation (High Priority)

**Files:**
- `src/DotNetUp.Core/Steps/FileSystem/CopyFileStep.cs`
- `src/DotNetUp.Core/Steps/FileSystem/MoveFileStep.cs`
- `src/DotNetUp.Core/Steps/FileSystem/DeleteFileStep.cs`

**Example Implementation (CopyFileStep):**

```csharp
public class CopyFileStep : IInstallationStep
{
    public string Name => "CopyFile";
    public string Description => $"Copy {SourcePath} to {DestinationPath}";

    public string SourcePath { get; }
    public string DestinationPath { get; }
    public bool Overwrite { get; }

    private string? _backupPath;

    public CopyFileStep(string sourcePath, string destinationPath, bool overwrite = false)
    {
        SourcePath = sourcePath;
        DestinationPath = destinationPath;
        Overwrite = overwrite;
    }

    public Task<InstallationResult> ValidateAsync(InstallationContext context)
    {
        // Check source exists
        // Check destination directory exists
        // Check permissions
        // If !Overwrite, check destination doesn't exist
        // Return result
    }

    public Task<InstallationResult> ExecuteAsync(InstallationContext context)
    {
        // If destination exists and Overwrite, backup it
        // Copy source to destination
        // Store backup path for rollback
        // Return result
    }

    public Task<InstallationResult> RollbackAsync(InstallationContext context)
    {
        // If backup exists, restore it
        // Otherwise delete destination
        // Best effort - log but don't fail
        // Return result
    }
}
```

---

### 3. Unit Tests (Critical for Quality)

**Target:** 80%+ code coverage

**Test Structure:**
```
tests/DotNetUp.Tests/
  ├── Fixtures/
  │   └── TestInstallationContext.cs  (Helper for creating test contexts)
  ├── Models/
  │   ├── InstallationResultTests.cs
  │   └── InstallationContextTests.cs
  ├── Builders/
  │   └── InstallationBuilderTests.cs
  ├── Execution/
  │   └── InstallationTests.cs  (Executor tests)
  └── Steps/
      └── FileSystem/
          ├── CopyFileStepTests.cs
          ├── MoveFileStepTests.cs
          └── DeleteFileStepTests.cs
```

**Testing Patterns:**

For each step, test:
1. **Validate success** - All prerequisites met
2. **Validate failure** - Missing source file, no permissions, etc.
3. **Execute success** - Operation completes
4. **Execute failure** - Source locked, disk full, etc.
5. **Rollback success** - Changes undone
6. **Rollback failure** - Best-effort logging
7. **Edge cases** - Empty paths, special characters, long paths

**Mocking:**
- Use `Moq` for ILogger
- Consider `System.IO.Abstractions` for filesystem mocking
- Or use temporary test directories and real file operations

**Test Fixture Example:**
```csharp
public class TestInstallationContext
{
    public static InstallationContext Create(
        ILogger? logger = null,
        Dictionary<string, object>? properties = null)
    {
        var mockLogger = logger ?? new Mock<ILogger>().Object;
        var context = new InstallationContext(mockLogger);

        if (properties != null)
        {
            foreach (var kvp in properties)
                context.Properties[kvp.Key] = kvp.Value;
        }

        return context;
    }
}
```

---

### 4. Additional Package References (As Needed)

May need to add:
```xml
<!-- For filesystem abstractions/mocking -->
<PackageReference Include="System.IO.Abstractions" Version="21.0.0" />

<!-- For tests -->
<PackageReference Include="Moq" Version="4.20.70" />
<PackageReference Include="FluentAssertions" Version="6.12.0" />
```

---

### 5. Documentation

**README.md** (Create):
- What is DotNetUp?
- Quick start example
- Installation steps
- Basic usage
- Contributing guidelines

**Example Usage** to include in README:
```csharp
using DotNetUp.Core.Builders;
using DotNetUp.Core.Steps.FileSystem;
using Microsoft.Extensions.Logging;

var logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger("Installer");

var installation = new InstallationBuilder()
    .WithLogger(logger)
    .WithStep(new CopyFileStep("source.txt", "destination.txt"))
    .WithStep(new CopyFileStep("app.exe", "C:\\Program Files\\MyApp\\app.exe"))
    .Build();

var executor = new Installation(installation.Steps, installation.Context);
var result = await executor.ExecuteAsync();

if (result.Success)
{
    Console.WriteLine("Installation completed successfully!");
}
else
{
    Console.WriteLine($"Installation failed: {result.Message}");
}
```

---

## 📋 Priority Order for Next Session

1. **Installation Executor** (Execution/Installation.cs) - Core orchestration
2. **CopyFileStep** (Steps/FileSystem/CopyFileStep.cs) - First concrete step
3. **Basic Tests** for executor and CopyFileStep
4. **Get green build** with tests passing
5. **MoveFileStep** and **DeleteFileStep**
6. **Complete test coverage**
7. **README.md** with examples

---

## 🎯 Definition of Done for Phase 1 MVP

- [ ] All interfaces implemented
- [ ] InstallationBuilder fluent API works
- [ ] Installation executor with rollback works
- [ ] At least 3 FileSystem steps (Copy, Move, Delete)
- [ ] Unit tests with 80%+ coverage
- [ ] All tests passing on Raspberry Pi runner
- [ ] README with usage examples
- [ ] Can perform a complete file-based installation with rollback

---

## 🛠️ Development Workflow (Reminder)

Since you're using Claude Code web without local .NET SDK:

1. Write code in web editor
2. Commit and push to `claude/**` branch
3. GitHub Actions automatically runs build on Raspberry Pi
4. Check build status via API or GitHub web UI
5. If build fails, check logs and fix
6. If build succeeds, continue to next component
7. Repeat until feature complete

**Build Status Command:**
```bash
curl -H "Authorization: token $GITHUB_TOKEN" \
  "https://api.github.com/repos/SzymonWelter/dotNETup/actions/runs?branch=claude/setup-dotnet-library-WXrJ5&per_page=1"
```

---

## 📝 Notes

- Keep commits atomic and focused
- Always include XML documentation comments
- Follow the "Results not Exceptions" principle
- Test both success and failure paths
- Best-effort rollback (log failures but continue)
- Check CancellationToken in long-running operations

---

**Last Updated:** December 15, 2025
**Status:** Phase 1 MVP - 40% Complete
**Next Milestone:** Installation Executor + First FileSystem Step
