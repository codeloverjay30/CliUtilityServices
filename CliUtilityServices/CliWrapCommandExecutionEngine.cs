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
    private const string PipeCleanupExceptionDataKey =
        "CliUtilityServices.PipeCleanupException";

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
        CommandLineInputValidator.ValidateForExecution(
            commandLineInput);

        string executablePath =
            _executableResolver.Resolve(
                commandLineInput.Command);

        IReadOnlyDictionary<string, string?>
            environmentMutations =
                _environmentResolver.Resolve(
                    commandLineInput.EnvironmentPolicy,
                    commandLineInput.EnvironmentVariables);

        ICommandPipeStrategy pipeStrategy =
            commandLineInput.PipeStrategy;

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

        Exception? primaryException = null;
        Stopwatch? stopwatch = null;

        try
        {
            command =
                pipeStrategy.ConfigurePipes(
                    command,
                    commandLineInput.OutputEncoding);

            stopwatch =
                Stopwatch.StartNew();

            CliWrap.CommandResult rawResult;

            try
            {
                rawResult =
                    await command.ExecuteAsync(
                            cancellationToken)
                        .ConfigureAwait(false);
            }
            finally
            {
                stopwatch.Stop();
            }

            (
                string standardOutput,
                string standardError
            ) = await pipeStrategy
                .GetResultAsync()
                .ConfigureAwait(false);

            return new CommandExecutionResult(
                StandardOutput: standardOutput,
                StandardError: standardError,
                ExitCode: rawResult.ExitCode,
                RunTime: stopwatch.Elapsed);
        }
        catch (Exception exception)
        {
            primaryException = exception;
            throw;
        }
        finally
        {
            stopwatch?.Stop();

            await CleanupPipeStrategyAsync(
                    pipeStrategy,
                    primaryException)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Cleans execution-scoped pipe resources while preserving the primary
    /// command exception when both command execution and cleanup fail.
    /// </summary>
    /// <param name="pipeStrategy">
    /// The command pipe strategy associated with the execution.
    /// </param>
    /// <param name="primaryException">
    /// The primary exception raised by command configuration, execution, or
    /// result retrieval, or <see langword="null"/> when the primary operation
    /// completed successfully.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous cleanup operation.
    /// </returns>
    private static async Task CleanupPipeStrategyAsync(
        ICommandPipeStrategy pipeStrategy,
        Exception? primaryException)
    {
        if (pipeStrategy is not IExecutionScopedPipeStrategy
            executionScopedPipeStrategy)
        {
            return;
        }

        try
        {
            await executionScopedPipeStrategy
                .CleanupAsync()
                .ConfigureAwait(false);
        }
        catch (Exception cleanupException)
            when (primaryException is not null)
        {
            /*
             * Preserve the original execution failure as the thrown exception.
             * The secondary cleanup failure remains available for diagnostics
             * without replacing the primary exception type or stack trace.
             */
            primaryException.Data[
                PipeCleanupExceptionDataKey] =
                cleanupException;
        }
    }
}