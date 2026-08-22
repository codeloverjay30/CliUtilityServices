using Commands.Infrastructure;

namespace CliUtilityServices;

/// <summary>
/// Defines the low-level command execution engine used by command executors.
/// </summary>
public interface ICommandExecutionEngine
{
    /// <summary>
    /// Executes the specified command configuration.
    /// </summary>
    /// <param name="commandLineInput">The command execution configuration.</param>
    /// <param name="cancellationToken">The cancellation token used to stop execution.</param>
    /// <returns>The command execution result.</returns>
    Task<CommandExecutionResult> ExecuteAsync(
        CommandLineInput commandLineInput,
        CancellationToken cancellationToken);
}