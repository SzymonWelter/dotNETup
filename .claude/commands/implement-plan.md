---
allowed-tools: Read, Write, Edit, Bash, Task, TaskOutput, TodoWrite
argument-hint: [spec-file] [additional-prompt]
description: Implements a feature from specification using planning, development, and review agents
---

# Implementation Workflow

You are orchestrating a multi-agent workflow to implement a feature based on a specification file.

## Input
- Arguments: $ARGUMENTS

Parse the arguments as follows:
1. **Specification file**: The first argument (file path to the spec)
2. **Additional prompt**: Everything after the first argument (optional custom instructions)

Example usages:
- `/implement-plan docs/feature-spec.md` - just the spec file
- `/implement-plan docs/feature-spec.md "Focus on performance optimization"` - with additional instructions
- `/implement-plan docs/feature-spec.md Skip registry operations, use only file system steps` - with additional context

The additional prompt (if provided) MUST be included in every agent invocation to ensure consistent guidance throughout the workflow.

## Workflow Steps

### Step 1: Read the Specification
First, read the specification file provided by the user to understand what needs to be implemented.

### Step 2: Planning Phase
Invoke the `implementation-planner` agent with the following prompt:

```
Analyze the following specification and create a detailed implementation plan.

Specification file: [include the spec file path]
[include the full content of the spec file]

## Additional Instructions (if provided)
[include the additional prompt here, or state "None provided" if empty]

Create a step-by-step implementation plan that:
1. Identifies all files that need to be created or modified
2. Defines the order of implementation (respecting dependencies)
3. Lists specific tasks with clear acceptance criteria
4. Identifies any potential risks or edge cases
5. Suggests test cases that should be written

IMPORTANT: If additional instructions were provided above, incorporate them into your planning.

Output the plan in a structured format that can be followed by a developer.
```

Wait for the planning agent to complete and capture its output.

### Step 3: Implementation Phase
Invoke the `dotnet-developer` agent with the implementation plan:

```
Implement the following plan based on the specification.

Original Specification:
[include the spec file content]

Implementation Plan:
[include the output from the planning agent]

## Additional Instructions (if provided)
[include the additional prompt here, or state "None provided" if empty]

Instructions:
1. Follow the implementation plan step by step
2. Write idiomatic C# code following .NET best practices
3. Implement all required interfaces, classes, and methods
4. Write unit tests for each component
5. Ensure the code compiles without errors
6. Run tests to verify implementation

IMPORTANT: If additional instructions were provided above, follow them throughout implementation.

After implementation, provide a summary of:
- Files created/modified
- Key implementation decisions made
- Any deviations from the plan and why
```

Wait for the developer agent to complete.

### Step 4: Review Phase
Invoke the `implementation-reviewer` agent to review the implementation:

```
Review the implementation that was just completed.

Original Specification:
[include the spec file content]

Implementation Plan:
[include the planning output]

Implementation Summary:
[include the developer agent's summary]

## Additional Instructions (if provided)
[include the additional prompt here, or state "None provided" if empty]

Review the implementation for:
1. Adherence to the specification requirements
2. Adherence to the implementation plan
3. Adherence to any additional instructions provided above
4. SOLID principles compliance
5. DRY (Don't Repeat Yourself) adherence
6. KISS (Keep It Simple, Stupid) principle
7. Code quality and maintainability
8. Test coverage adequacy
9. Potential bugs or edge cases

IMPORTANT: If additional instructions were provided, verify the implementation follows them.

Provide specific, actionable feedback for any issues found.
Format your feedback as a list of items that need to be addressed.
```

### Step 5: Iteration (if needed)
If the reviewer provides feedback that requires changes, invoke the `dotnet-developer` agent again:

```
The implementation reviewer has provided the following feedback:

[include the reviewer's feedback]

## Additional Instructions (if provided)
[include the additional prompt here, or state "None provided" if empty]

Please address each item in the feedback:
1. Fix any issues identified
2. Refactor code as suggested
3. Add missing tests if noted
4. Ensure all changes compile and tests pass

IMPORTANT: Continue to follow any additional instructions while making fixes.

Provide a summary of changes made to address each feedback item.
```

After fixes are applied, you may optionally run another review cycle if significant changes were made.

### Step 6: Final Summary
Once the implementation is complete and reviewed, provide a final summary to the user:

1. What was implemented
2. Files created/modified
3. Tests added
4. Any notable decisions or trade-offs
5. Build and test status

## Important Notes

- Run agents sequentially, not in parallel - each phase depends on the previous
- Capture and pass context between agents accurately
- If any agent fails, report the failure and stop the workflow
- Keep the user informed of progress between phases
- Use TodoWrite to track overall workflow progress
