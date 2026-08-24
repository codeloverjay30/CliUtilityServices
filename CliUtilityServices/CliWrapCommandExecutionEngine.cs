using System.Diagnostics;
using CliUtilityServices.Pipes;
using CliUtilityServices.Security;
using CliWrap;
using Commands.Infrastructure;

namespace CliUtilityServices;

/// <summary>
/// Executes command-line processes through CliWrap.
/// </summary>
public sealed class CliWrapCommandExecutionEngine : ICommandExecutionEngine
{
private readonly IExecutableResolver _executableResolver;
private readonly IChildEnvironmentResolver _environmentResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="CliWrapCommandExecutionEngine"/> class.
    /// </summary>
    /// <param name="executableResolver">The executable resolver.</param>
    /// <param name="environmentResolver">
    /// The child-process environment resolver.
    /// </param>
    public CliWrapCommandExecutionEngine(
        IExecutableResolver executableResolver,
        IChildEnvironmentResolver environmentResolver)
    {
        ArgumentNullException.ThrowIfNull(executableResolver);
        ArgumentNullException.ThrowIfNull(environmentResolver);

        _executableResolver = executableResolver;
        _environmentResolver = environmentResolver;
    }


    /// <inheritdoc />
    public async Task<CommandExecutionResult> ExecuteAsync(
        CommandLineInput commandLineInput,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commandLineInput);

        string executablePath =
            _executableResolver.Resolve(
                commandLineInput.Command);

        IReadOnlyDictionary<string, string?> environmentMutations =
            _environmentResolver.Resolve(
                commandLineInput.EnvironmentPolicy,
                commandLineInput.EnvironmentVariables);

        ICommandPipeStrategy pipeStrategy =
            commandLineInput.PipeStrategy
            ?? new SlidingWindowPipeStrategy(500);

        Command command = Cli.Wrap(executablePath)
            .WithArguments(commandLineInput.Arguments)
            .WithEnvironmentVariables(environmentMutations)
            .WithValidation(commandLineInput.Validation);

        if (!string.IsNullOrWhiteSpace(
                commandLineInput.WorkingDirectory))
        {
            command = command.WithWorkingDirectory(
                commandLineInput.WorkingDirectory);
        }

        command = pipeStrategy.ConfigurePipes(
            command,
            commandLineInput.OutputEncoding);

        var stopwatch = Stopwatch.StartNew();

        CliWrap.CommandResult rawResult;

        try
        {
            rawResult =
                await command.ExecuteAsync(
                    cancellationToken);
        }
        finally
        {
            stopwatch.Stop();
        }

        (
            string standardOutput,
            string standardError
        ) = await pipeStrategy.GetResultAsync();

        return new CommandExecutionResult(
            StandardOutput: standardOutput,
            StandardError: standardError,
            ExitCode: rawResult.ExitCode,
            RunTime: stopwatch.Elapsed);
    }
}