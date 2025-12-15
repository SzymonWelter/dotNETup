using DotNetUp.Core.Interfaces;
using DotNetUp.Core.Models;
using Microsoft.Extensions.Logging;

namespace DotNetUp.Core.Execution;

/// <summary>
/// Orchestrates the execution of installation steps with validation, execution, and rollback.
/// </summary>
public class Installation
{
    private readonly IReadOnlyList<IInstallationStep> _steps;
    private readonly InstallationContext _context;

    /// <summary>
    /// Creates a new installation executor.
    /// </summary>
    /// <param name="steps">List of steps to execute</param>
    /// <param name="context">Installation context</param>
    /// <exception cref="ArgumentNullException">Thrown when steps or context is null</exception>
    /// <exception cref="ArgumentException">Thrown when steps list is empty</exception>
    public Installation(IReadOnlyList<IInstallationStep> steps, InstallationContext context)
    {
        _steps = steps ?? throw new ArgumentNullException(nameof(steps));
        _context = context ?? throw new ArgumentNullException(nameof(context));

        if (steps.Count == 0)
            throw new ArgumentException("At least one step is required", nameof(steps));
    }

    /// <summary>
    /// Executes the installation by validating all steps, executing them in sequence,
    /// and rolling back if any step fails.
    /// </summary>
    /// <returns>Result indicating success or failure</returns>
    /// <exception cref="OperationCanceledException">Thrown when cancellation is requested</exception>
    public async Task<InstallationResult> ExecuteAsync()
    {
        _context.Logger.LogInformation("Starting installation with {StepCount} steps", _steps.Count);

        // Phase 1: Validate all steps upfront (fail fast)
        var validationResult = await ValidateAllStepsAsync();
        if (!validationResult.Success)
        {
            _context.Logger.LogError("Validation failed: {Message}", validationResult.Message);
            return validationResult;
        }

        // Phase 2: Execute steps sequentially
        var executedSteps = new List<IInstallationStep>();

        for (int i = 0; i < _steps.Count; i++)
        {
            var step = _steps[i];
            var stepNumber = i + 1;

            // Check for cancellation
            _context.CancellationToken.ThrowIfCancellationRequested();

            // Set current step context
            _context.SetCurrentStep(stepNumber, _steps.Count, step.Name);

            _context.Logger.LogInformation(
                "Executing step {StepNumber}/{TotalSteps}: {StepName}",
                stepNumber, _steps.Count, step.Name);

            // Execute the step
            var result = await step.ExecuteAsync(_context);
            executedSteps.Add(step);

            if (!result.Success)
            {
                _context.Logger.LogError(
                    "Step {StepNumber}/{TotalSteps} ({StepName}) failed: {Message}",
                    stepNumber, _steps.Count, step.Name, result.Message);

                // Trigger rollback
                await RollbackAsync(executedSteps);

                return InstallationResult.FailureResult(
                    $"Installation failed at step {stepNumber} ({step.Name}): {result.Message}",
                    result.Exception);
            }

            _context.Logger.LogInformation(
                "Step {StepNumber}/{TotalSteps} ({StepName}) completed successfully",
                stepNumber, _steps.Count, step.Name);
        }

        _context.Logger.LogInformation("Installation completed successfully");
        return InstallationResult.SuccessResult("Installation completed successfully");
    }

    /// <summary>
    /// Validates all steps before execution (fail fast approach).
    /// </summary>
    /// <returns>Result indicating whether all validations passed</returns>
    private async Task<InstallationResult> ValidateAllStepsAsync()
    {
        _context.Logger.LogInformation("Validating all steps...");

        for (int i = 0; i < _steps.Count; i++)
        {
            var step = _steps[i];
            var stepNumber = i + 1;

            _context.Logger.LogDebug(
                "Validating step {StepNumber}/{TotalSteps}: {StepName}",
                stepNumber, _steps.Count, step.Name);

            var result = await step.ValidateAsync(_context);

            if (!result.Success)
            {
                _context.Logger.LogError(
                    "Validation failed for step {StepNumber}/{TotalSteps} ({StepName}): {Message}",
                    stepNumber, _steps.Count, step.Name, result.Message);

                return InstallationResult.FailureResult(
                    $"Validation failed for step {stepNumber} ({step.Name}): {result.Message}",
                    result.Exception);
            }
        }

        _context.Logger.LogInformation("All steps validated successfully");
        return InstallationResult.SuccessResult("All steps validated successfully");
    }

    /// <summary>
    /// Rolls back executed steps in reverse order (best-effort).
    /// Failures during rollback are logged but don't stop the rollback process.
    /// </summary>
    /// <param name="executedSteps">List of steps that were successfully executed</param>
    private async Task RollbackAsync(List<IInstallationStep> executedSteps)
    {
        if (executedSteps.Count == 0)
        {
            _context.Logger.LogInformation("No steps to roll back");
            return;
        }

        _context.Logger.LogWarning(
            "Rolling back {StepCount} executed steps in reverse order...",
            executedSteps.Count);

        // Reverse the list to rollback in LIFO order
        executedSteps.Reverse();

        foreach (var step in executedSteps)
        {
            try
            {
                _context.Logger.LogInformation("Rolling back step: {StepName}", step.Name);

                var rollbackResult = await step.RollbackAsync(_context);

                if (!rollbackResult.Success)
                {
                    // Log warning but continue (best-effort rollback)
                    _context.Logger.LogWarning(
                        "Rollback failed for step {StepName}: {Message}. Continuing with remaining rollbacks...",
                        step.Name, rollbackResult.Message);
                }
                else
                {
                    _context.Logger.LogInformation(
                        "Step {StepName} rolled back successfully",
                        step.Name);
                }
            }
            catch (Exception ex)
            {
                // Catch exceptions during rollback and log, but continue (best-effort)
                _context.Logger.LogError(
                    ex,
                    "Exception occurred while rolling back step {StepName}. Continuing with remaining rollbacks...",
                    step.Name);
            }
        }

        _context.Logger.LogWarning("Rollback completed (best-effort)");
    }
}
