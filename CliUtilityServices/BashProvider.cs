// File: CliUtilityServices/Terminals/BashProvider.cs
using System.IO.Abstractions;
using System.Runtime.InteropServices;
using System.Text;
using EnvironmentUtilityServices;

namespace CliUtilityServices.Terminals;

public class BashProvider : ITerminalProvider
{
    private readonly IFileSystem _fileSystem;
    public string TerminalName => "bash";

    public TerminalTypeOptions TerminalType => TerminalTypeOptions.Bash;
    public Encoding DefaultEncoding => Encoding.UTF8;

    public BashProvider(
        IFileSystem fileSystem
    )
    {
        ArgumentNullException.ThrowIfNull(fileSystem);

        _fileSystem = fileSystem;
    }
    public string GetExecutablePath(
        IEnvironmentService environmentService
    )
    {
        if (environmentService.IsWindows())
        {
            // Windows 環境下預設尋找 Git Bash
            string gitBash = @"C:\Program Files\Git\bin\bash.exe";
            return _fileSystem.File.Exists(gitBash) ? gitBash : "bash.exe";
        }
        return "bash";
    }

    public IEnumerable<string> BuildArgs(string rawCommand)
    {
        return new[] { "-c", rawCommand };
    }
}