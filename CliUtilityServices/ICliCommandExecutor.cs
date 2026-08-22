using Commands.Infrastructure;

namespace CliUtilityServices;

/// <summary>
/// Defines a contract for executing command-line processes.
/// </summary>
public interface ICliCommandExecutor : ISystemCommandExecutor
{
    /// <summary>
    /// Executes the command using a terminal selected for the current platform.
    /// </summary>
    /// <param name="commandLineInput">The command execution configuration.</param>
    /// <param name="cancellationToken">
    /// A token used to cancel the command execution.
    /// </param>
    /// <returns>The command execution result.</returns>
    Task<CommandExecutionResult> ExecuteAutoDetectedAsync(
        CommandLineInput commandLineInput,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the command using the specified terminal.
    /// </summary>
    /// <param name="terminalType">The terminal implementation to use.</param>
    /// <param name="commandLineInput">The command execution configuration.</param>
    /// <param name="cancellationToken">
    /// A token used to cancel the command execution.
    /// </param>
    /// <returns>The command execution result.</returns>
    Task<CommandExecutionResult> ExecuteInShellAsync(
        TerminalTypeOptions terminalType,
        CommandLineInput commandLineInput,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an executable directly without an intermediate shell interpreter.
    /// </summary>
    /// <param name="commandLineInput">The process execution configuration.</param>
    /// <param name="cancellationToken">
    /// A token used to cancel the process.
    /// </param>
    /// <returns>The command execution result.</returns>
    Task<CommandExecutionResult> ExecuteProcessAsync(
        CommandLineInput commandLineInput,
        CancellationToken cancellationToken = default);
}