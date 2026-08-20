using System.IO.Abstractions;
using System.Runtime.InteropServices;
using CliUtilityServices.Pipes;
using CliUtilityServices.Terminals;
using CliWrap;
using CommandResult.Infrastructure;
using Commands.Infrastructure;
using EnvironmentUtilityServices;
using OsVersionUtilityServices;

namespace CliUtilityServices;

/// <summary>
/// The implementation of <see cref="global::CliUtilityServices.ICliCommandExecutor"/> 
/// </summary>
public class CliCommandExecutor : ICliCommandExecutor
{
    /// <summary>
    /// The oldest major version that not use bash terminal as default terminal in MacOs OS. 
    /// </summary>
    private const int MajorVersionThatNotUseBashAsDefaultForMacOS = 18;
    private readonly ICliResultProcessor _resultProcessor;

    private readonly IFileSystem _fileSystem;
    private readonly IEnvironmentService _environmentService;
    private readonly IOSVersionResolver _osVersionResolver;
    private readonly Dictionary<TerminalTypeOptions, ITerminalProvider> _terminalProviders = new Dictionary<TerminalTypeOptions, ITerminalProvider>();
    public CliCommandExecutor(
        IFileSystem fileSystem,
        IEnvironmentService environmentService,
        IOSVersionResolver osVersionResolver,
        ICliResultProcessor resultProcessor
    )
    {
        ArgumentNullException.ThrowIfNull(fileSystem, nameof(fileSystem));
        ArgumentNullException.ThrowIfNull(environmentService, nameof(environmentService));
        ArgumentNullException.ThrowIfNull(osVersionResolver, nameof(osVersionResolver));
        ArgumentNullException.ThrowIfNull(resultProcessor, nameof(resultProcessor));


        _fileSystem = fileSystem;
        _environmentService = environmentService;
        _osVersionResolver = osVersionResolver;
        _resultProcessor = resultProcessor;

        var providers = new ITerminalProvider[]
        {
            new CmdProvider(_fileSystem),
            new PowerShellProvider(_fileSystem),
            new PowerShellCoreProvider(_fileSystem),
            new BashProvider(_fileSystem),
            new ZshProvider(_fileSystem),
        };
        _terminalProviders = providers.ToDictionary(p => p.TerminalType, p => p);
    }

    /// <inheritdoc/>
    public async Task<CommandExecutionResult> ExecuteInShellAsync(
        TerminalTypeOptions terminalType,
        string command,
        IEnumerable<string> arguments
    )
    {
        // 這裡可以根據 terminalType 來決定使用哪個 TerminalProvider
        // 例如，如果 terminalType 是 Cmd，則使用 CmdProvider 來執行命令
        // 這裡假設你已經有一個方法 GetTerminalProvider(terminalType) 可以返回對應的 ITerminalProvider
        var terminalProvider = GetTerminalProvider(terminalType);

        CommandLineInput commandLineInput = new CommandLineInputBuilder()
            .WithCommand(command)
            .WithArguments(arguments)
            .WithDefaultEncoding(terminalProvider.DefaultEncoding)
            .Build();

        // 將原始結果轉換為 CommandExecutionResult
        return await ExecuteInShellAsync(commandLineInput);
    }

    /// <inheritdoc/>
    public async Task<CommandExecutionResult> ExecuteInShellAsync(
        string command,
        IEnumerable<string> arguments
    )
    {
        CommandLineInput commandLineInput = new CommandLineInputBuilder()
            .WithCommand(command)
            .WithArguments(arguments)
            .Build();

        return await ExecuteInShellAsync(commandLineInput);
    }

    /// <inheritdoc/>
    public async Task<CommandExecutionResult> ExecuteInShellAsync(
        CommandLineInput commandLineInput
    )
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

        return _resultProcessor.Process(result);
    }

    /// <inheritdoc/>
    public async Task<CommandExecutionResult> ExecuteAutoDetectedAsync(
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
            if (version.Major < MajorVersionThatNotUseBashAsDefaultForMacOS)
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

    /// <inheritdoc/>
    public async Task<CommandExecutionResult> ExecuteInShellAsync(TerminalTypeOptions terminalType, CommandLineInput commandLineInput)
    {
        if (!_terminalProviders.TryGetValue(terminalType, out var provider))
        {
            throw new NotSupportedException($"Terminal type '{terminalType}' is not supported.");
        }

        commandLineInput = commandLineInput with
        {
            Command = provider.GetExecutablePath(_environmentService),
        };

        return await ExecuteInShellAsync(commandLineInput);
    }


    /// <summary>
    /// Get <see cref="global::CliUtilityServices.Terminals.ITerminalProvider"/> instance given <paramref name="terminalType"/> which type is <see cref="global::CliUtilityServices.TerminalTypeOptions"/>
    /// </summary>
    /// <param name="terminalType">terminal type to determine which terminal is used</param>
    /// <returns></returns>
    /// <exception cref="NotSupportedException">When <paramref name="terminalType"/> is NOT supported (i.e. it is not in <see cref="_terminalProviders"/>)</exception>
    private ITerminalProvider GetTerminalProvider(TerminalTypeOptions terminalType)
    {
        if (!_terminalProviders.TryGetValue(terminalType, out var provider))
        {
            throw new NotSupportedException($"Terminal type '{terminalType}' is not supported.");
        }
        return provider;
    }
}
