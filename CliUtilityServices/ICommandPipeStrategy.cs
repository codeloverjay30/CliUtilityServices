using System.Text;
using CliWrap;

namespace CliUtilityServices.Pipes;

/// <summary>
/// Defines a defensive strategy for handling CLI standard and error output streams.
/// </summary>
public interface ICommandPipeStrategy
{
    /// <summary>
    /// Configures the standard output and error pipes for the specified command.
    /// </summary>
    /// <param name="command">The CliWrap command to configure.</param>
    /// <param name="encoding">The encoding to use for the streams.</param>
    /// <returns>A configured command instance.</returns>
    Command ConfigurePipes(Command command, Encoding encoding);

    /// <summary>
    /// Synchronously or asynchronously retrieves the captured result after command execution.
    /// </summary>
    /// <returns>A tuple containing (StandardOutput, StandardError).</returns>
    Task<(string StandardOutput, string StandardError)> GetResultAsync();
}