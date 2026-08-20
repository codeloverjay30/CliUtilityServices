using System.ComponentModel;
using System.IO.Abstractions;
using System.Runtime.InteropServices;
using System.Text;
using CliUtilityServices.Pipes;
using CliUtilityServices.Terminals;
using CliWrap;
using CliWrap.Buffered;
using CustomDataAnnotations.Maintenance;
using EnvironmentUtilityServices;
using OsVersionUtilityServices;

namespace CliUtilityServices;

/// <summary>
/// The contract of running commands using terminal
/// </summary>
/// <remarks>
/// It violates SRP as its methods return <see cref="global::CliWrap.Buffered.BufferedCommandResult"/> 
/// which is tigt-coupled with <see cref="global::CliWrap"/> package.
/// Use <see cref="global::CliUtilityServices.CliCommandExecutor"/> 
/// which its most of public methods both return <see cref="global::Commands.Infrastructure.CommandExecutionResult"/>
/// </remarks>
[TechnicalDebt(CategoryType.CodeSmell | CategoryType.LowRigidityIssue | CategoryType.ViolateSrpIssue, "ICliCommandExecutor")]
[Obsolete("""
It violates SRP as its methods return <see cref="global::CliWrap.Buffered.BufferedCommandResult"/> 
which is tigt-coupled with <see cref="global::CliWrap"/> package.
Use <see cref="global::CliUtilityServices.CliCommandExecutor"/> 
which its most of public methods both return <see cref="global::Commands.Infrastructure.CommandExecutionResult"/>
""")]
[EditorBrowsable(EditorBrowsableState.Never)]

public class CliWrapRunner : ICommandLineRunner
{
    private readonly IFileSystem _fileSystem;
    private readonly IEnvironmentService _environmentService;
    private readonly IOSVersionResolver _osVersionResolver;
    private readonly Dictionary<TerminalTypeOptions, ITerminalProvider> _terminalProviders;

    public IEnumerable<TerminalTypeOptions> SupportedTerminalTypes => _terminalProviders.Keys;

    public IEnumerable<ITerminalProvider> SupportedTerminalProviders => throw new NotImplementedException();

    static CliWrapRunner()
    {
        // 註冊編碼表
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public CliWrapRunner(
        IFileSystem fileSystem,
        IEnvironmentService environmentService,
        IOSVersionResolver osVersionResolver
    )
    {
        ArgumentNullException.ThrowIfNull(fileSystem, nameof(fileSystem));
        ArgumentNullException.ThrowIfNull(environmentService, nameof(environmentService));
        ArgumentNullException.ThrowIfNull(osVersionResolver, nameof(osVersionResolver));

        _fileSystem = fileSystem;
        _environmentService = environmentService;
        _osVersionResolver = osVersionResolver;

        // 註冊所有支援的 Terminal 策略
        var providers = new ITerminalProvider[] {
            new CmdProvider(_fileSystem),
            new PowerShellProvider(_fileSystem),
            new PowerShellCoreProvider(_fileSystem),
            new BashProvider(_fileSystem),
            new ZshProvider(_fileSystem),
        };
        _terminalProviders = providers.ToDictionary(p => p.TerminalType, p => p);
    }

    /// <summary>
    /// Execute command with arguments (or without arguments) according <paramref name="commandLineInput"/>
    /// </summary>
    /// <param name="terminalType">determine to use which terminal</param>
    /// <param name="commandLineInput"><see cref="global::CliUtilityServices.CommandLineInput"/></param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException">Thrown when <paramref name="terminalType"/>is NOT one of supported terminal</exception>

    public async Task<BufferedCommandResult> ExecuteInShellAsync(
        TerminalTypeOptions terminalType,
        CommandLineInput commandLineInput
    )
    {
        if (!_terminalProviders.TryGetValue(terminalType, out var provider))
        {
            throw new NotSupportedException($"Terminal type '{terminalType}' is not supported.");
        }

        var input = new CommandLineInput
        {
            EnvironmentService = _environmentService,
            Command = provider.GetExecutablePath(_environmentService),
            Arguments = commandLineInput.Arguments,
            WorkingDirectory = commandLineInput.WorkingDirectory,
            OutputEncoding = commandLineInput.OutputEncoding,
            InputEncoding = commandLineInput.InputEncoding,
            Validation = commandLineInput.Validation
        };

        return await ExecuteAsync(input);
    }

    /// <summary>
    /// Executes the command using a terminal automatically selected based on the current operating system.
    /// </summary>
    /// <param name="commandLineInput">The input configuration.</param>
    /// <returns>The command result.</returns>
    public async Task<BufferedCommandResult> ExecuteAutoDetectedAsync(
        CommandLineInput commandLineInput
    )
    {
        // 自動偵測邏輯：Windows 預設使用 Cmd，非 Windows 預設使用 Bash
        TerminalTypeOptions terminalType;
        if (_environmentService.IsWindows())
        {
            terminalType = TerminalTypeOptions.Cmd;
        }
        else if (_environmentService.IsLinux())
        {
            terminalType = TerminalTypeOptions.Bash;
        }
        else if (_environmentService.IsMacOS())
        {
            Version version = _osVersionResolver.Resolve(RuntimeInformation.OSDescription);
            if (version.Major <= 18)
            {
                terminalType = TerminalTypeOptions.Bash;
            }
            else
            {
                terminalType = TerminalTypeOptions.Zsh;
            }
        }
        else
        {
            // Fallback
            terminalType = TerminalTypeOptions.Bash;
        }

        return await ExecuteInShellAsync(terminalType, commandLineInput);
    }

    /// <summary>
    /// Helper method of <see cref="global::CliUtilityServices.CliWrapRunner.ExecuteInShellAsync(TerminalTypeOptions, CommandLineInput)"/>
    /// </summary>
    /// <param name="terminalType"></param>
    /// <param name="rawCommand">raw command that will be executed</param>
    /// <param name="workingDirectory">the working directory that you want to change to</param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException"></exception>
    /// <remarks>
    /// This method is NOT safe since it can't avoid arguments injection attack
    /// Consider use <see cref="global::CliUtilityServices.CliWrapRunner.ExecuteInShellAsync(TerminalTypeOptions, CommandLineInput)" />
    /// </remarks>
    [Obsolete("This method is unsafe, consider use global::CliUtilityServices.CliWrapRunner.ExecuteInShellAsync(TerminalTypeOptions, CommandLineInput)")]
    [TechnicalDebt(CategoryType.SecurityVulnerability | CategoryType.CodeSmell | CategoryType.OutdatedStrategy, "global::CliUtilityServices.CliWrapRunner.ExecuteInShellAsync(TerminalTypeOptions, CommandLineInput)")]
    public async Task<BufferedCommandResult> ExecuteInShellAsync(
        TerminalTypeOptions terminalType,
        string rawCommand,
        string workingDirectory = ""
    )
    {
        if (!_terminalProviders.TryGetValue(terminalType, out var provider))
        {
            throw new NotSupportedException($"Terminal type '{terminalType}' is not supported.");
        }

        var input = new CommandLineInput
        {
            EnvironmentService = _environmentService,
            Command = provider.GetExecutablePath(_environmentService),
            Arguments = provider.BuildArgs(rawCommand), // 透過策略組裝安全參數
            WorkingDirectory = workingDirectory,
            OutputEncoding = provider.DefaultEncoding,
            Validation = CommandResultValidation.ZeroExitCode
        };

        return await ExecuteAsync(input);
    }

    /// <inheritdoc cref="global::CliUtilityServices.CliWrapRunner.ExecuteInShellAsync(TerminalTypeOptions, CommandLineInput)" />
    /// <param name="commandLineInput"><see cref="global::CliUtilityServices.CommandLineInput"/></param>
    /// <returns></returns>
    /// <remarks>
    /// This method is NOT safe since it can't avoid command injection attack and arguments injection attack
    /// Consider only used it internally (in fact, I use it internally in this utility package)
    /// But for backforward-compatibility, I choose it as `public` instead of `private`.
    /// Consider use <see cref="global::CliUtilityServices.CliWrapRunner.ExecuteInShellAsync(TerminalTypeOptions, CommandLineInput)" />
    /// </remarks>
    [Obsolete("This method is unsafe and smells bad, consider use global::CliUtilityServices.CliWrapRunner.ExecuteInShellAsync(TerminalTypeOptions, CommandLineInput)")]
    [TechnicalDebt(CategoryType.SecurityVulnerability | CategoryType.CodeSmell | CategoryType.OutdatedStrategy, "global::CliUtilityServices.CliWrapRunner.ExecuteInShellAsync(TerminalTypeOptions, CommandLineInput)")]
    public async Task<BufferedCommandResult> ExecuteAsync(CommandLineInput commandLineInput)
    {
        var strategy = commandLineInput.PipeStrategy ?? new SlidingWindowPipeStrategy(500);

        // 從輸入參數中取得指定的輸出編碼
        var targetEncoding = commandLineInput.OutputEncoding;

        // 使用你定義的擴充方法 ExecuteWithEncodingAsync 來處理指令執行與編碼轉換
        // 該方法內部會處理 PipeTarget.ToStringBuilder 並回傳包含輸出的結果
        var cli = Cli.Wrap(commandLineInput.Command)
                     .WithArguments(commandLineInput.Arguments);
        if (!string.IsNullOrWhiteSpace(commandLineInput.WorkingDirectory))
        {
            cli = cli.WithWorkingDirectory(commandLineInput.WorkingDirectory);
        }
        cli = cli.WithValidation(commandLineInput.Validation);
        cli = strategy.ConfigurePipes(cli, targetEncoding);
        var result = await cli.ExecuteWithEncodingAsync(targetEncoding);

        return result;
    }
}