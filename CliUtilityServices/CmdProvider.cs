using System.IO.Abstractions;
using System.Runtime.InteropServices;
using System.Text;
using EnvironmentUtilityServices;

namespace CliUtilityServices.Terminals;

public class CmdProvider : ITerminalProvider
{
    private readonly IFileSystem _fileSystem;
    public string TerminalName => "cmd";
    public TerminalTypeOptions TerminalType => TerminalTypeOptions.Cmd;
    public Encoding DefaultEncoding => Encoding.GetEncoding("Big5"); // 針對台灣 Windows 預設環境

    public CmdProvider(
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
        if (!environmentService.IsWindows())
        {
            throw new PlatformNotSupportedException("cmd.exe is only supported on Windows.");
        }
        return _fileSystem.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
    }

    public IEnumerable<string> BuildArgs(string rawCommand)
    {
        // /c 代表執行完後關閉視窗
        return new[] { "/c", rawCommand };
    }
}
