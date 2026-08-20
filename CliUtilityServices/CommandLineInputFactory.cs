using System;
using System.IO;
using System.IO.Abstractions;
using System.Runtime.InteropServices;
using System.Text;
using CustomDataAnnotations.Maintenance;
using EnvironmentUtilityServices;

namespace CliUtilityServices;

/// <inheritdoc cref="global::CliUtilityServices.Terminals.ITerminalProvider"/>
[Obsolete("This method is unsafe and smells bad, consider use BuildArgs method defined in the class that implements ITerminalProvider to build args")]
[TechnicalDebt(CategoryType.SecurityVulnerability | CategoryType.CodeSmell | CategoryType.OutdatedStrategy , "global::CliUtilityServices.Terminals.ITerminalProvider")]
public class CommandLineInputFactory
{
    private readonly string? _customWindowsCmdPath;
    private readonly string? _customWindowsBashPath;

    private readonly IFileSystem _fileSystem;
    private readonly IEnvironmentService _environmentService;

    static CommandLineInputFactory()
    {
        // 註冊編碼表
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }
    public CommandLineInputFactory(
        IFileSystem fileSystem,
        IEnvironmentService environmentService,
        string? customWindowsCmdPath = null,
        string? customWindowsBashPath = null
    )
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(environmentService);

        _fileSystem = fileSystem;
        _environmentService = environmentService;

        _customWindowsCmdPath = customWindowsCmdPath;
        _customWindowsBashPath = customWindowsBashPath;
    }

    /// <summary>
    /// According to current OS type, create corresponding `CommandLineInput` instance.
    /// </summary>
    [Obsolete("This method is unsafe and smells bad, consider use BuildArgs method defined in the class -- `CmdProvider` to build args")]
    [TechnicalDebt(CategoryType.SecurityVulnerability | CategoryType.CodeSmell | CategoryType.OutdatedStrategy , "global::CliUtilityServices.Terminals.ITerminalProvider")]
    public CommandLineInput CreateShellInput(
        string arguments,
        string workingDirectory = ""
    )
    {
        bool isWindows = _environmentService.IsWindows();

        string fileName;
        string argumentPrefix;
        Encoding defaultEncoding;

        if (isWindows)
        {
            // 如果有指定特定資料夾的 cmd.exe，就使用它；否則用系統預設
            fileName = !string.IsNullOrEmpty(_customWindowsCmdPath)
                ? _customWindowsCmdPath
                : _fileSystem.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");

            argumentPrefix = "/c";
            defaultEncoding = Encoding.GetEncoding("Big5");
        }
        else
        {
            // 非 Windows 平台 (Linux / macOS) 通常直接呼叫 bash
            fileName = "bash";
            argumentPrefix = "-c";
            defaultEncoding = Encoding.UTF8;
        }

        return new CommandLineInput
        {
            EnvironmentService = _environmentService,
            Command = fileName,
            Arguments = new List<string>() { $"{argumentPrefix}", $"{arguments}" },
            WorkingDirectory = workingDirectory,
            InputEncoding = defaultEncoding,
            OutputEncoding = defaultEncoding
        };
    }

    /// <summary>
    /// requirement: Use Bash Terminal on Windows.
    /// </summary>
    [Obsolete("This method is unsafe and smells bad, consider use BuildArgs method defined in the class -- `BashProvider` to build args")]
    [TechnicalDebt(CategoryType.SecurityVulnerability | CategoryType.CodeSmell | CategoryType.OutdatedStrategy, "global::CliUtilityServices.Terminals.BashProvider")]
    public CommandLineInput CreateWindowsBashInput(
        string arguments,
        string workingDirectory = ""
    )
    {
        if (!_environmentService.IsWindows())
        {
            throw new PlatformNotSupportedException("This method only supports on Windows.");
        }

        // 如果沒有指定特定資料夾的 bash.exe，則嘗試常見的 Git Bash 預設路徑
        string fileName = !string.IsNullOrEmpty(_customWindowsBashPath)
            ? _customWindowsBashPath
            : @"C:\Program Files\Git\bin\bash.exe";

        return new CommandLineInput
        {
            EnvironmentService = _environmentService,
            Command = fileName,
            Arguments = new List<string>() { $"-c", $"{arguments}" },
            WorkingDirectory = workingDirectory,
            InputEncoding = Encoding.UTF8,
            OutputEncoding = Encoding.UTF8
        };
    }
}