using CliUtilityServices.Pipes;
using CliUtilityServices.Security;

namespace CliUtilityServices;

/// <summary>
/// Validates command-line input invariants required by process execution.
/// </summary>
internal static class CommandLineInputValidator
{
    /// <summary>
    /// Validates a command-line input before any execution-side dependency is used.
    /// </summary>
    /// <param name="input">The command-line input to validate.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the input or a required reference is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the command is null, empty, or whitespace.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the timeout is zero or negative.
    /// </exception>
    public static void ValidateForExecution(
        CommandLineInput input)
    {
        ArgumentNullException.ThrowIfNull(
            input,
            nameof(input));

        ArgumentException.ThrowIfNullOrWhiteSpace(
            input.Command,
            nameof(input.Command));

        ArgumentNullException.ThrowIfNull(
            input.Arguments,
            nameof(input.Arguments));

        ArgumentNullException.ThrowIfNull(
            input.PipeStrategy,
            nameof(input.PipeStrategy));

        ArgumentNullException.ThrowIfNull(
            input.EnvironmentPolicy,
            nameof(input.EnvironmentPolicy));

        ArgumentNullException.ThrowIfNull(
            input.EnvironmentVariables,
            nameof(input.EnvironmentVariables));

        if (input.Timeout is { } timeout &&
            timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(input.Timeout),
                timeout,
                "Timeout must be greater than zero.");
        }
    }
}
