using CliWrap.Buffered;
using Commands.Infrastructure;

namespace CliUtilityServices;

/// <summary>
/// contract of cli command executor
/// </summary>
public interface ICliCommandExecutor : ISystemCommandExecutor
{
    /// <summary>
    /// Executes the command with configuration <paramref name="commandLineInput"/> using a terminal automatically selected based on the current operating system.
    /// </summary>
    /// <param name="commandLineInput">The command configuration.</param>
    Task<CommandExecutionResult> ExecuteAutoDetectedAsync(CommandLineInput commandLineInput);

    /// <summary>
    /// Execute the command with configuration <paramref name="commandLineInput"/> using special terminal <paramref name="terminalType"/>
    /// </summary>
    /// <param name="terminalType">terminal type</param>
    /// <param name="commandLineInput">The command configuration.</param>
    /// <returns></returns>
    Task<CommandExecutionResult> ExecuteInShellAsync(TerminalTypeOptions terminalType, CommandLineInput commandLineInput);

}
