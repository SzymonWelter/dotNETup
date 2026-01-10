using System.Diagnostics;
using DotNetUp.Core.Interfaces;
using DotNetUp.Core.Models;
using Microsoft.Extensions.Logging;

namespace DotNetUp.Core.Execution;

/// <summary>
/// Orchestrates the execution of installation steps with validation, execution, and rollback.
/// Focuses on high-level flow and delegates step execution to IStepExecutor.
/// </summary>
internal class Installation : IInstallation
{
    private readonly IReadOnlyList<ConfiguredStep> _steps;
    private readonly InstallationContext _context;
    private readonly InstallationOptions _options;
    private readonly IStepExecutor _stepExecutor;

    /// <summary>
    /// Creates a new installation executor.
    /// </summary>
    /// <param name="steps">List of configured steps to execute</param>
    /// <param name="context">Installation context</param>
    /// <param name="options">Installation options</param>
    /// <exception cref="ArgumentNullException">Thrown when steps, context, or options is null</exception>
    /// <exception cref="ArgumentException">Thrown when steps list is empty</exception>
    public Installation(IReadOnlyList<ConfiguredStep> steps, InstallationContext context, InstallationOptions options)
    {
        _steps = steps ?? throw new ArgumentNullException(nameof(steps));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (steps.Count == 0)
            throw new ArgumentException("At least one step is required", nameof(steps));

        _stepExecutor = StepExecutorFactory.Create(context.Logger);
    }

    /// <inheritdoc />
    public async Task<InstallationSummary> InstallAsync()
    {
        _context.IsUninstall = false;
        return await ExecuteStepsAsync(_steps.ToList(), "Installation");
    }

    /// <inheritdoc />
    public async Task<InstallationSummary> UninstallAsync()
    {
        _context.IsUninstall = true;
        var reversedSteps = _steps.Reverse().ToList();
        return await UninstallStepsAsync(reversedSteps);
    }

    /// <inheritdoc />
    public async Task<InstallationSummary> RepairAsync(params string[] stepNames)
    {
        _context.IsUninstall = false;

        List<ConfiguredStep> stepsToExecute;
        if (stepNames == null || stepNames.Length == 0)
        {
            stepsToExecute = _steps.ToList();
        }
        else
        {
            stepsToExecute = _steps
                .Where(s => stepNames.Contains(s.Name, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (stepsToExecute.Count == 0)
            {
                return InstallationSummary.FailureSummary(
                    "No matching steps found for repair",
                    string.Empty,
                    new Dictionary<string, InstallationStepResult>(),
                    TimeSpan.Zero);
            }
        }

        return await ExecuteStepsAsync(stepsToExecute, "Repair");
    }

    /// <summary>
    /// Executes the given steps with validation, execution, and rollback.
    /// </summary>
    private async Task<InstallationSummary> ExecuteStepsAsync(List<ConfiguredStep> steps, string operationName)
    {
        var stopwatch = Stopwatch.StartNew();
        var stepResults = new Dictionary<string, InstallationStepResult>();

        _context.Logger.LogInformation("Starting {Operation} with {StepCount} steps", operationName, steps.Count);

        // Check for admin requirement
        if (_options.RequireAdministrator && !IsRunningAsAdministrator())
        {
            stopwatch.Stop();
            return InstallationSummary.FailureSummary(
                $"{operationName} requires administrator privileges",
                string.Empty,
                stepResults,
                stopwatch.Elapsed);
        }

        // Phase 1: Validate all steps upfront (if enabled)
        if (_options.ValidateBeforeInstall)
        {
            var validationResult = await ValidateAllStepsAsync(steps);
            if (!validationResult.Success)
            {
                _context.Logger.LogError("Validation failed: {Message}", validationResult.Message);
                stopwatch.Stop();
                return InstallationSummary.FailureSummary(
                    validationResult.Message,
                    string.Empty,
                    stepResults,
                    stopwatch.Elapsed,
                    validationResult.Exception);
            }
        }

        // Phase 2: Execute steps sequentially
        var executedSteps = new List<ConfiguredStep>();
        string? failedStepName = null;

        using var timeoutCts = new CancellationTokenSource(_options.Timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            _context.CancellationToken, timeoutCts.Token);

        try
        {
            for (int i = 0; i < steps.Count; i++)
            {
                var configuredStep = steps[i];
                var stepNumber = i + 1;

                // Check for cancellation/timeout
                linkedCts.Token.ThrowIfCancellationRequested();

                // Check ShouldSkip condition
                if (await ShouldSkipStepAsync(configuredStep))
                {
                    _context.Logger.LogInformation(
                        "Skipping step {StepNumber}/{TotalSteps}: {StepName} (condition met)",
                        stepNumber, steps.Count, configuredStep.Name);
                    continue;
                }

                // Set current step context
                _context.SetCurrentStep(stepNumber, steps.Count, configuredStep.Name);

                _context.Logger.LogInformation(
                    "Executing step {StepNumber}/{TotalSteps}: {StepName}",
                    stepNumber, steps.Count, configuredStep.Name);

                // Execute the step using the executor chain
                var result = await _stepExecutor.ExecuteAsync(configuredStep, _context, linkedCts.Token);
                stepResults[configuredStep.Name] = result;
                executedSteps.Add(configuredStep);

                if (!result.Success)
                {
                    // Check if we should continue on error
                    if (configuredStep.Options.ContinueOnError)
                    {
                        _context.Logger.LogWarning(
                            "Step {StepNumber}/{TotalSteps} ({StepName}) failed but ContinueOnError is set: {Message}",
                            stepNumber, steps.Count, configuredStep.Name, result.Message);
                        continue;
                    }

                    failedStepName = configuredStep.Name;
                    _context.Logger.LogError(
                        "Step {StepNumber}/{TotalSteps} ({StepName}) failed: {Message}",
                        stepNumber, steps.Count, configuredStep.Name, result.Message);

                    // Trigger rollback if enabled
                    if (_options.RollbackOnFailure)
                    {
                        await RollbackAsync(executedSteps);
                    }

                    stopwatch.Stop();
                    return InstallationSummary.FailureSummary(
                        $"{operationName} failed at step {stepNumber} ({configuredStep.Name}): {result.Message}",
                        configuredStep.Name,
                        stepResults,
                        stopwatch.Elapsed,
                        result.Exception);
                }

                _context.Logger.LogInformation(
                    "Step {StepNumber}/{TotalSteps} ({StepName}) completed successfully",
                    stepNumber, steps.Count, configuredStep.Name);
            }

            _context.Logger.LogInformation("{Operation} completed successfully", operationName);
            stopwatch.Stop();
            return InstallationSummary.SuccessSummary(
                $"{operationName} completed successfully",
                stepResults,
                stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _context.Logger.LogWarning("{Operation} timed out after {Timeout}", operationName, _options.Timeout);

            if (_options.RollbackOnFailure)
            {
                await RollbackAsync(executedSteps);
            }

            stopwatch.Stop();
            return InstallationSummary.FailureSummary(
                $"{operationName} timed out after {_options.Timeout}",
                failedStepName ?? string.Empty,
                stepResults,
                stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            _context.Logger.LogWarning("{Operation} cancelled by user", operationName);

            if (_options.RollbackOnFailure)
            {
                await RollbackAsync(executedSteps);
            }

            // Rethrow to propagate cancellation
            throw;
        }
        finally
        {
            // Always dispose all executed steps to clean up temporary resources
            // This ensures backup files and other temp resources are cleaned up
            // even when ContinueOnError=true or rollback wasn't called
            await DisposeStepsAsync(executedSteps);
        }
    }

    /// <summary>
    /// Executes uninstallation by calling RollbackAsync on each step in reverse order.
    /// Uninstallation is kept simple - no retries, no special execution strategies.
    /// If a step fails, it logs and continues with the remaining steps.
    /// </summary>
    private async Task<InstallationSummary> UninstallStepsAsync(List<ConfiguredStep> steps)
    {
        var stopwatch = Stopwatch.StartNew();
        var stepResults = new Dictionary<string, InstallationStepResult>();

        _context.Logger.LogInformation("Starting Uninstallation with {StepCount} steps (reverse order)", steps.Count);

        // Check for admin requirement
        if (_options.RequireAdministrator && !IsRunningAsAdministrator())
        {
            stopwatch.Stop();
            return InstallationSummary.FailureSummary(
                "Uninstallation requires administrator privileges",
                string.Empty,
                stepResults,
                stopwatch.Elapsed);
        }

        using var timeoutCts = new CancellationTokenSource(_options.Timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            _context.CancellationToken, timeoutCts.Token);

        string? failedStepName = null;
        var hasCriticalFailures = false;
        var executedSteps = new List<ConfiguredStep>();

        try
        {
            for (int i = 0; i < steps.Count; i++)
            {
                var configuredStep = steps[i];
                var stepNumber = i + 1;

                // Check for cancellation/timeout
                linkedCts.Token.ThrowIfCancellationRequested();

                // Check ShouldSkip condition
                if (await ShouldSkipStepAsync(configuredStep))
                {
                    _context.Logger.LogInformation(
                        "Skipping uninstall step {StepNumber}/{TotalSteps}: {StepName} (condition met)",
                        stepNumber, steps.Count, configuredStep.Name);
                    continue;
                }

                // Set current step context
                _context.SetCurrentStep(stepNumber, steps.Count, configuredStep.Name);

                _context.Logger.LogInformation(
                    "Uninstalling step {StepNumber}/{TotalSteps}: {StepName}",
                    stepNumber, steps.Count, configuredStep.Name);

                // Execute RollbackAsync directly - keep uninstall simple
                try
                {
                    var result = await configuredStep.Step.RollbackAsync(_context);
                    stepResults[configuredStep.Name] = result;
                    executedSteps.Add(configuredStep);

                    if (!result.Success)
                    {
                        // Only track as critical failure if ContinueOnError is not set
                        if (!configuredStep.Options.ContinueOnError)
                        {
                            hasCriticalFailures = true;
                            failedStepName ??= configuredStep.Name;
                        }
                        _context.Logger.LogWarning(
                            "Uninstall step {StepNumber}/{TotalSteps} ({StepName}) failed: {Message}. Continuing with remaining steps...",
                            stepNumber, steps.Count, configuredStep.Name, result.Message);
                    }
                    else
                    {
                        _context.Logger.LogInformation(
                            "Uninstall step {StepNumber}/{TotalSteps} ({StepName}) completed successfully",
                            stepNumber, steps.Count, configuredStep.Name);
                    }
                }
                catch (Exception ex)
                {
                    // Only track as critical failure if ContinueOnError is not set
                    if (!configuredStep.Options.ContinueOnError)
                    {
                        hasCriticalFailures = true;
                        failedStepName ??= configuredStep.Name;
                    }
                    stepResults[configuredStep.Name] = InstallationStepResult.FailureResult(ex.Message, ex);
                    _context.Logger.LogError(
                        ex,
                        "Uninstall step {StepNumber}/{TotalSteps} ({StepName}) threw exception. Continuing with remaining steps...",
                        stepNumber, steps.Count, configuredStep.Name);
                }
            }

            stopwatch.Stop();

            if (hasCriticalFailures)
            {
                _context.Logger.LogWarning("Uninstallation completed with errors");
                return InstallationSummary.FailureSummary(
                    "Uninstallation completed with errors",
                    failedStepName ?? string.Empty,
                    stepResults,
                    stopwatch.Elapsed);
            }

            _context.Logger.LogInformation("Uninstallation completed successfully");
            return InstallationSummary.SuccessSummary(
                "Uninstallation completed successfully",
                stepResults,
                stopwatch.Elapsed);
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            _context.Logger.LogWarning("Uninstallation timed out after {Timeout}", _options.Timeout);

            stopwatch.Stop();
            return InstallationSummary.FailureSummary(
                $"Uninstallation timed out after {_options.Timeout}",
                failedStepName ?? string.Empty,
                stepResults,
                stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            _context.Logger.LogWarning("Uninstallation cancelled by user");
            throw;
        }
        finally
        {
            // Always dispose all executed steps to clean up temporary resources
            await DisposeStepsAsync(executedSteps);
        }
    }

    /// <summary>
    /// Checks if a step should be skipped based on its ShouldSkip condition.
    /// </summary>
    private async Task<bool> ShouldSkipStepAsync(ConfiguredStep configuredStep)
    {
        if (configuredStep.Options.ShouldSkip == null)
            return false;

        return await configuredStep.Options.ShouldSkip(_context);
    }

    /// <summary>
    /// Validates all steps before execution (fail fast approach).
    /// </summary>
    private async Task<InstallationStepResult> ValidateAllStepsAsync(List<ConfiguredStep> steps)
    {
        _context.Logger.LogInformation("Validating all steps...");

        for (int i = 0; i < steps.Count; i++)
        {
            var configuredStep = steps[i];
            var stepNumber = i + 1;

            // Check ShouldSkip - if step will be skipped, don't validate it
            if (await ShouldSkipStepAsync(configuredStep))
            {
                _context.Logger.LogDebug(
                    "Skipping validation for step {StepNumber}/{TotalSteps}: {StepName} (will be skipped)",
                    stepNumber, steps.Count, configuredStep.Name);
                continue;
            }

            _context.Logger.LogDebug(
                "Validating step {StepNumber}/{TotalSteps}: {StepName}",
                stepNumber, steps.Count, configuredStep.Name);

            var result = await configuredStep.Step.ValidateAsync(_context);

            if (!result.Success)
            {
                // Check if we should continue on error for validation too
                if (configuredStep.Options.ContinueOnError)
                {
                    _context.Logger.LogWarning(
                        "Validation failed for step {StepNumber}/{TotalSteps} ({StepName}) but ContinueOnError is set: {Message}",
                        stepNumber, steps.Count, configuredStep.Name, result.Message);
                    continue;
                }

                _context.Logger.LogError(
                    "Validation failed for step {StepNumber}/{TotalSteps} ({StepName}): {Message}",
                    stepNumber, steps.Count, configuredStep.Name, result.Message);

                return InstallationStepResult.FailureResult(
                    $"Validation failed for step {stepNumber} ({configuredStep.Name}): {result.Message}",
                    result.Exception);
            }
        }

        _context.Logger.LogInformation("All steps validated successfully");
        return InstallationStepResult.SuccessResult("All steps validated successfully");
    }

    /// <summary>
    /// Rolls back executed steps in reverse order (best-effort).
    /// Rollback is kept simple - no retries, no timeouts per step.
    /// Failures during rollback are logged but don't stop the rollback process.
    /// </summary>
    private async Task RollbackAsync(List<ConfiguredStep> executedSteps)
    {
        if (executedSteps.Count == 0)
        {
            _context.Logger.LogInformation("No steps to roll back");
            return;
        }

        _context.Logger.LogWarning(
            "Rolling back {StepCount} executed steps in reverse order...",
            executedSteps.Count);

        // Create a reversed copy to avoid modifying the original list
        var stepsToRollback = executedSteps.AsEnumerable().Reverse().ToList();

        foreach (var configuredStep in stepsToRollback)
        {
            try
            {
                _context.Logger.LogInformation("Rolling back step: {StepName}", configuredStep.Name);

                var rollbackResult = await configuredStep.Step.RollbackAsync(_context);

                if (!rollbackResult.Success)
                {
                    // Log warning but continue (best-effort rollback)
                    _context.Logger.LogWarning(
                        "Rollback failed for step {StepName}: {Message}. Continuing with remaining rollbacks...",
                        configuredStep.Name, rollbackResult.Message);
                }
                else
                {
                    _context.Logger.LogInformation(
                        "Step {StepName} rolled back successfully",
                        configuredStep.Name);
                }
            }
            catch (Exception ex)
            {
                // Catch exceptions during rollback and log, but continue (best-effort)
                _context.Logger.LogError(
                    ex,
                    "Exception occurred while rolling back step {StepName}. Continuing with remaining rollbacks...",
                    configuredStep.Name);
            }
        }

        _context.Logger.LogWarning("Rollback completed (best-effort)");
    }

    /// <summary>
    /// Disposes all executed steps to clean up temporary resources.
    /// Called in finally block to ensure cleanup happens regardless of success/failure.
    /// Handles ContinueOnError scenarios where rollback is not called.
    /// </summary>
    private async Task DisposeStepsAsync(List<ConfiguredStep> executedSteps)
    {
        if (executedSteps.Count == 0)
        {
            return;
        }

        _context.Logger.LogDebug("Disposing {StepCount} executed steps to clean up temporary resources", executedSteps.Count);

        foreach (var configuredStep in executedSteps)
        {
            try
            {
                await configuredStep.Step.DisposeAsync();
            }
            catch (Exception ex)
            {
                // Best-effort: log but don't fail on disposal errors
                _context.Logger.LogWarning(
                    ex,
                    "Failed to dispose step {StepName}. Temporary resources may be orphaned.",
                    configuredStep.Name);
            }
        }

        _context.Logger.LogDebug("Step disposal completed");
    }

    /// <summary>
    /// Checks if the current process is running with administrator privileges.
    /// </summary>
    private static bool IsRunningAsAdministrator()
    {
        if (OperatingSystem.IsWindows())
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }

        // On Unix-like systems, check if running as root (UID 0)
        return Environment.GetEnvironmentVariable("USER") == "root" ||
               Environment.GetEnvironmentVariable("EUID") == "0";
    }
}
