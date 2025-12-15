namespace DotNetUp.Core.Models;

/// <summary>
/// Represents the result of an installation operation.
/// Used by all installation steps to return execution status consistently.
/// </summary>
public class InstallationResult
{
    /// <summary>
    /// Indicates whether the operation completed successfully.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// A human-readable message describing the operation result.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// The exception that occurred during the operation, if any.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Additional data associated with the operation result.
    /// Can be used to pass information between steps or return operation-specific data.
    /// </summary>
    public Dictionary<string, object> Data { get; init; } = new();

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="message">Success message</param>
    /// <param name="data">Optional data dictionary</param>
    /// <returns>A successful InstallationResult</returns>
    public static InstallationResult SuccessResult(string message, Dictionary<string, object>? data = null)
    {
        return new InstallationResult
        {
            Success = true,
            Message = message,
            Data = data ?? new Dictionary<string, object>()
        };
    }

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    /// <param name="message">Failure message</param>
    /// <param name="exception">Optional exception that caused the failure</param>
    /// <param name="data">Optional data dictionary</param>
    /// <returns>A failed InstallationResult</returns>
    public static InstallationResult FailureResult(string message, Exception? exception = null, Dictionary<string, object>? data = null)
    {
        return new InstallationResult
        {
            Success = false,
            Message = message,
            Exception = exception,
            Data = data ?? new Dictionary<string, object>()
        };
    }
}
