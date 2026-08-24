using System.ComponentModel;
using Commands.Infrastructure;
using CustomDataAnnotations.Maintenance;

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
    /// <remarks>
    /// Use <see cref="global::CliUtilityServices.ICliCommandExecutor.ExecuteProcessAsync(CommandLineInput, CancellationToken)"/> method for direct process execution or ExecuteTrustedScriptAsync for trusted shell scripts.".
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("""Use <see cref="global::CliUtilityServices.ICliCommandExecutor.ExecuteProcessAsync(CommandLineInput, CancellationToken)"/> method instead""", error: false)]
    [TechnicalDebt(CategoryType.CodeSmell|CategoryType.SecurityVulnerability,"""Use <see cref="global::CliUtilityServices.ICliCommandExecutor.ExecuteProcessAsync(CommandLineInput, CancellationToken)"/> method instead""")]
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

    /// <summary>
    /// Executes explicitly trusted script text through the specified shell interpreter.
    /// </summary>
    /// <remarks>
    /// This API intentionally crosses a shell interpretation boundary.
    /// The supplied script must not contain untrusted user-controlled script text.
    /// </remarks>
    /// <param name="terminalType">
    /// The shell interpreter used to execute the script.
    /// </param>
    /// <param name="trustedScript">
    /// Trusted shell script text.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel execution.
    /// </param>
    /// <returns>The command execution result.</returns>
    Task<CommandExecutionResult> ExecuteTrustedScriptAsync(
        TerminalTypeOptions terminalType,
        string trustedScript,
        CancellationToken cancellationToken = default);
}