using System.ComponentModel;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Runtime.InteropServices;
using CliUtilityServices.Pipes;
using CliUtilityServices.Terminals;
using CliWrap;
using CommandResult.Infrastructure;
using Commands.Infrastructure;
using CustomDataAnnotations.Maintenance;
using EnvironmentUtilityServices;
using OsVersionUtilityServices;

namespace CliUtilityServices;

/// <summary>
/// Provides defensive cross-platform command and process execution services.
/// </summary>
public sealed class CliCommandExecutor : ICliCommandExecutor
{
    private const int MajorVersionThatNotUseBashAsDefaultForMacOS = 18;

    private readonly ICommandExecutionEngine _executionEngine;

    private readonly IFileSystem _fileSystem;
    private readonly IEnvironmentService _environmentService;
    private readonly IOSVersionResolver _osVersionResolver;

    private readonly IReadOnlyDictionary<TerminalTypeOptions, ITerminalProvider>
        _terminalProviders;

    /// <summary>
    /// Initializes a new instance of the <see cref="CliCommandExecutor"/> class.
    /// </summary>
    /// <param name="fileSystem">The file system abstraction.</param>
    /// <param name="environmentService">The environment abstraction.</param>
    /// <param name="osVersionResolver">The operating system version resolver.</param>
    /// <param name="executionEngine">The low-level command execution engine.</param>
    public CliCommandExecutor(
        IFileSystem fileSystem,
        IEnvironmentService environmentService,
        IOSVersionResolver osVersionResolver,
        ICommandExecutionEngine executionEngine)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(environmentService);
        ArgumentNullException.ThrowIfNull(osVersionResolver);
        ArgumentNullException.ThrowIfNull(executionEngine);

        _fileSystem = fileSystem;
        _environmentService = environmentService;
        _osVersionResolver = osVersionResolver;
        _executionEngine = executionEngine;

        ITerminalProvider[] providers =
        [
            new CmdProvider(_fileSystem),
            new PowerShellProvider(_fileSystem),
            new PowerShellCoreProvider(_fileSystem),
            new BashProvider(_fileSystem),
            new ZshProvider(_fileSystem)
        ];

        _terminalProviders = providers.ToDictionary(
            provider => provider.TerminalType,
            provider => provider);
    }

    /// <summary>
    /// Executes an executable directly without invoking a shell interpreter.
    /// </summary>
    /// <param name="commandLineInput">The process execution configuration.</param>
    /// <param name="cancellationToken">
    /// A token used to cancel the process execution.
    /// </param>
    /// <returns>The process execution result.</returns>
    public async Task<CommandExecutionResult> ExecuteProcessAsync(
        CommandLineInput commandLineInput,
        CancellationToken cancellationToken = default)
    {
        CommandLineInputValidator.ValidateForExecution(
            commandLineInput);

        using var timeoutCancellationTokenSource =
            CreateTimeoutCancellationTokenSource(commandLineInput.Timeout);

        using var linkedCancellationTokenSource =
            CreateLinkedCancellationTokenSource(
                cancellationToken,
                timeoutCancellationTokenSource?.Token);

        CancellationToken effectiveCancellationToken =
            linkedCancellationTokenSource?.Token
            ?? timeoutCancellationTokenSource?.Token
            ?? cancellationToken;

        try
        {
            return await _executionEngine.ExecuteAsync(
                commandLineInput,
                effectiveCancellationToken);
        }
        catch (OperationCanceledException)
            when (
                timeoutCancellationTokenSource?.IsCancellationRequested == true
                && !cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Command '{commandLineInput.Command}' exceeded the configured timeout of '{commandLineInput.Timeout}'.");
        }
    }

    /// <summary>
    /// Executes the supplied input using a platform-appropriate terminal.
    /// </summary>
    /// <param name="commandLineInput">The command configuration.</param>
    /// <param name="cancellationToken">
    /// A token used to cancel execution.
    /// </param>
    /// <returns>The command execution result.</returns>
    public Task<CommandExecutionResult> ExecuteAutoDetectedAsync(
        CommandLineInput commandLineInput,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commandLineInput);

        TerminalTypeOptions terminalType = ResolveDefaultTerminal();

        return ExecuteInShellAsync(
            terminalType,
            commandLineInput,
            cancellationToken);
    }

    /// <summary>
    /// Executes the supplied input using the specified shell.
    /// </summary>
    /// <remarks>
    /// Shell execution should only be used when shell semantics are explicitly required.
    /// Untrusted script text must not be passed directly to a shell interpreter.
    /// </remarks>
    /// <param name="terminalType">The terminal type.</param>
    /// <param name="commandLineInput">The command configuration.</param>
    /// <param name="cancellationToken">
    /// A token used to cancel execution.
    /// </param>
    /// <returns>The command execution result.</returns>
    public Task<CommandExecutionResult> ExecuteInShellAsync(
        TerminalTypeOptions terminalType,
        CommandLineInput commandLineInput,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(commandLineInput);

        ITerminalProvider provider = GetTerminalProvider(terminalType);

        CommandLineInput shellInput = commandLineInput with
        {
            Command = provider.GetExecutablePath(_environmentService),
            DefaultEncoding = provider.DefaultEncoding
        };

        return ExecuteProcessAsync(
            shellInput,
            cancellationToken);
    }

    /// <summary>
    /// Executes an executable and its arguments while preserving argument boundaries.
    /// </summary>
    /// <param name="command">The executable to execute.</param>
    /// <param name="arguments">The arguments passed to the executable.</param>
    /// <returns>The command execution result.</returns>
    /// <remarks>
    /// Use <see cref="global::CliUtilityServices.CliCommandExecutor.ExecuteProcessAsync(CommandLineInput, CancellationToken)"/> method for direct process execution or ExecuteTrustedScriptAsync for trusted shell scripts.".
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [Obsolete("""Use <see cref="global::CliUtilityServices.CliCommandExecutor.ExecuteProcessAsync(CommandLineInput, CancellationToken)"/> method instead""", error: false)]
    [TechnicalDebt(CategoryType.CodeSmell|CategoryType.SecurityVulnerability,"""Use <see cref="global::CliUtilityServices.CliCommandExecutor.ExecuteProcessAsync(CommandLineInput, CancellationToken)"/> method instead""")]
    public Task<CommandExecutionResult> ExecuteInShellAsync(
        string command,
        IEnumerable<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentNullException.ThrowIfNull(arguments);

        var commandLineInput = new CommandLineInputBuilder()
            .WithEnvironmentService(_environmentService)
            .WithCommand(command)
            .WithArguments(arguments)
            .Build();

        return ExecuteProcessAsync(commandLineInput);
    }
    

    /// <summary>
    /// Creates a cancellation source for the configured timeout.
    /// </summary>
    /// <param name="timeout">The optional timeout.</param>
    /// <returns>
    /// A configured cancellation source, or null when timeout is disabled.
    /// </returns>
    private static CancellationTokenSource?
        CreateTimeoutCancellationTokenSource(TimeSpan? timeout)
    {
        if (timeout is null)
        {
            return null;
        }

        var cancellationTokenSource = new CancellationTokenSource();

        cancellationTokenSource.CancelAfter(timeout.Value);

        return cancellationTokenSource;
    }

    /// <summary>
    /// Creates a linked cancellation source when both caller and timeout tokens exist.
    /// </summary>
    /// <param name="callerToken">The caller cancellation token.</param>
    /// <param name="timeoutToken">The optional timeout cancellation token.</param>
    /// <returns>
    /// A linked cancellation source when necessary; otherwise null.
    /// </returns>
    private static CancellationTokenSource?
        CreateLinkedCancellationTokenSource(
            CancellationToken callerToken,
            CancellationToken? timeoutToken)
    {
        if (!callerToken.CanBeCanceled || timeoutToken is null)
        {
            return null;
        }

        return CancellationTokenSource.CreateLinkedTokenSource(
            callerToken,
            timeoutToken.Value);
    }

    /// <summary>
    /// Resolves the default terminal for the current operating system.
    /// </summary>
    /// <returns>The platform-appropriate terminal type.</returns>
    private TerminalTypeOptions ResolveDefaultTerminal()
    {
        if (_environmentService.IsWindows())
        {
            return TerminalTypeOptions.Cmd;
        }

        if (_environmentService.IsLinux())
        {
            return TerminalTypeOptions.Bash;
        }

        if (_environmentService.IsMacOS())
        {
            Version version = _osVersionResolver.Resolve(
                RuntimeInformation.OSDescription);

            return version.Major < MajorVersionThatNotUseBashAsDefaultForMacOS
                ? TerminalTypeOptions.Bash
                : TerminalTypeOptions.Zsh;
        }

        throw new PlatformNotSupportedException(
            "Automatic terminal detection is not supported on the current operating system.");
    }

    /// <summary>
    /// Executes explicitly trusted script text through the specified shell interpreter.
    /// </summary>
    /// <remarks>
    /// This method intentionally invokes a shell interpreter.
    /// The supplied script must be trusted and must not be constructed from
    /// unvalidated user-controlled script fragments.
    /// </remarks>
    /// <param name="terminalType">
    /// The shell interpreter used to execute the trusted script.
    /// </param>
    /// <param name="trustedScript">
    /// Trusted shell script text.
    /// </param>
    /// <param name="cancellationToken">
    /// A token used to cancel execution.
    /// </param>
    /// <returns>The command execution result.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="trustedScript"/> is null, empty, or whitespace.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when <paramref name="terminalType"/> is not supported.
    /// </exception>
    public Task<CommandExecutionResult> ExecuteTrustedScriptAsync(
        TerminalTypeOptions terminalType,
        string trustedScript,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            trustedScript,
            nameof(trustedScript));

        ITerminalProvider provider =
            GetTerminalProvider(terminalType);

        var commandLineInput =
            new CommandLineInputBuilder()
                .WithEnvironmentService(
                    _environmentService)
                .WithCommand(
                    provider.GetExecutablePath(
                        _environmentService))
                .WithArguments(
                    provider.BuildArgs(
                        trustedScript))
                .WithDefaultEncoding(
                    provider.DefaultEncoding)
                .Build();

        return ExecuteProcessAsync(
            commandLineInput,
            cancellationToken);
    }

    /// <summary>
    /// Resolves a registered terminal provider.
    /// </summary>
    /// <param name="terminalType">The terminal type.</param>
    /// <returns>The registered terminal provider.</returns>
    /// <exception cref="NotSupportedException">
    /// Thrown when the requested terminal type is not supported.
    /// </exception>
    private ITerminalProvider GetTerminalProvider(
        TerminalTypeOptions terminalType)
    {
        if (_terminalProviders.TryGetValue(
                terminalType,
                out ITerminalProvider? provider))
        {
            return provider;
        }

        throw new NotSupportedException(
            $"Terminal type '{terminalType}' is not supported.");
    }
}