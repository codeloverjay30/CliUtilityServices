namespace Commands.Infrastructure;

/// <summary>
/// Defines a contract for executing system commands and retrieving their results.
/// </summary>
public interface ISystemCommandExecutor
{
    /// <summary>
    /// Executes a system command with the specified command and arguments, returning the result of the execution.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="arguments">The arguments for the command.</param>
    /// <returns>A task that represents the asynchronous operation and returns the execution result.</returns>
    Task<CommandExecutionResult> ExecuteInShellAsync(string command, IEnumerable<string> arguments);
}
