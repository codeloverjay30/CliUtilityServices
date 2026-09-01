namespace CliUtilityServices;

/// <summary>
/// Defines execution-scoped cleanup behavior for command pipe strategies
/// that own temporary resources.
/// </summary>
public interface IExecutionScopedPipeStrategy
{
    /// <summary>
    /// Releases resources created for the current command execution.
    /// </summary>
    Task CleanupAsync();
}
