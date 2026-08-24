using System.Diagnostics;
using CliUtilityServices.Pipes;
using CliUtilityServices.Security;
using CliWrap;
using Commands.Infrastructure;

namespace CliUtilityServices;

/// <summary>
/// Executes command-line processes through CliWrap.
/// </summary>
public sealed class CliWrapCommandExecutionEngine
    : ICommandExecutionEngine
{
    private readonly IExecutableResolver _executableResolver;
    private readonly IChildEnvironmentResolver _environmentResolver;
    private readonly IWorkingDirectoryResolver _workingDirectoryResolver;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="CliWrapCommandExecutionEngine"/> class.
/// </summary>
    /// <param name="executableResolver">
    /// The executable resolver.
/// </param>
    /// <param name="environmentResolver">
    /// The child-process environment resolver.
/// </param>
    /// <param name="workingDirectoryResolver">
    /// The child-process working-directory resolver.
/// </param>
    public CliWrapCommandExecutionEngine(
        IExecutableResolver executableResolver,
        IChildEnvironmentResolver environmentResolver,
        IWorkingDirectoryResolver workingDirectoryResolver)
    {
        ArgumentNullException.ThrowIfNull(
            executableResolver,
            nameof(executableResolver));

        ArgumentNullException.ThrowIfNull(
            environmentResolver,
            nameof(environmentResolver));

        ArgumentNullException.ThrowIfNull(
            workingDirectoryResolver,
            nameof(workingDirectoryResolver));

        _executableResolver = executableResolver;
        _environmentResolver = environmentResolver;
        _workingDirectoryResolver = workingDirectoryResolver;
    }

    /// <inheritdoc />
    public async Task<CommandExecutionResult> ExecuteAsync(
        CommandLineInput commandLineInput,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            commandLineInput,
            nameof(commandLineInput));

        string executablePath =
            _executableResolver.Resolve(
                commandLineInput.Command);

        IReadOnlyDictionary<string, string?>
            environmentMutations =
                _environmentResolver.Resolve(
                    commandLineInput.EnvironmentPolicy,
                    commandLineInput.EnvironmentVariables);

        ICommandPipeStrategy pipeStrategy =
            commandLineInput.PipeStrategy
            ?? new SlidingWindowPipeStrategy(500);

        Command command =
            Cli.Wrap(executablePath)
                .WithArguments(
                    commandLineInput.Arguments)
                .WithEnvironmentVariables(
                    environmentMutations)
                .WithValidation(
                    commandLineInput.Validation);

        if (!string.IsNullOrWhiteSpace(
                commandLineInput.WorkingDirectory))
        {
            string resolvedWorkingDirectory =
                _workingDirectoryResolver.Resolve(
                    commandLineInput.WorkingDirectory);

            command =
                command.WithWorkingDirectory(
                    resolvedWorkingDirectory);
        }

        command =
            pipeStrategy.ConfigurePipes(
                command,
                commandLineInput.OutputEncoding);

        var stopwatch =
            Stopwatch.StartNew();

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