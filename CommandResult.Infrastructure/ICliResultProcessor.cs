using CliWrap.Buffered;
using Commands.Infrastructure;

namespace CommandResult.Infrastructure;

/// <summary>
/// Defines a contract for processing raw CLI command results into domain-specific results.
/// </summary>
public interface ICliResultProcessor
{
    /// <summary>
    /// Processes and converts a raw CLI command result.
    /// </summary>
    /// <param name="bufferedCommandResult">The raw result from CliWrap.</param>
    /// <returns>A structured execution result.</returns>
    CommandExecutionResult Process(BufferedCommandResult bufferedCommandResult);
}
