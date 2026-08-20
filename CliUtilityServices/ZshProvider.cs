using System.IO.Abstractions;
using System.Text;
using EnvironmentUtilityServices;

namespace CliUtilityServices.Terminals;

/// <summary>
/// Provides terminal configuration for Zsh, primarily used on macOS.
/// </summary>
public class ZshProvider : ITerminalProvider
{
    private readonly IFileSystem _fileSystem;

    public string TerminalName => "zsh";
    public TerminalTypeOptions TerminalType => TerminalTypeOptions.Zsh;
    public Encoding DefaultEncoding => Encoding.UTF8;

    public ZshProvider(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }

    public string GetExecutablePath(IEnvironmentService environmentService)
    {
        // 防禦性檢查：Zsh 通常在 Unix-like 系統下位於 /bin/zsh
        return "/bin/zsh";
    }

    public IEnumerable<string> BuildArgs(string rawCommand)
    {
        return new[] { "-c", rawCommand };
    }
}