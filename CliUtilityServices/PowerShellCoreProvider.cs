// File: CliUtilityServices/Terminals/PowerShellProvider.cs
using System.IO.Abstractions;
using System.Runtime.InteropServices;
using System.Text;
using EnvironmentUtilityServices;

namespace CliUtilityServices.Terminals;

public class PowerShellCoreProvider : ITerminalProvider
{
    private readonly IFileSystem _fileSystem;
    public string TerminalName => "pwsh";

    public TerminalTypeOptions TerminalType => TerminalTypeOptions.PowerShellCore;
    // PowerShell Core 預設全面採用無 BOM 的 UTF-8
    public Encoding DefaultEncoding => Encoding.UTF8;

    public PowerShellCoreProvider(
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
        // 自動偵測跨平台的 pwsh (PowerShell Core)，若在 Windows 找不到則降級使用內建的 powershell.exe
        bool isWindows = environmentService.IsWindows();
        string preferredPath = isWindows ? "pwsh.exe" : "pwsh";

        // 這裡可藉由環境變數查找，簡單起見直接回傳名稱，CliWrap 會自動去 PATH 中尋找
        return preferredPath;
    }

    public IEnumerable<string> BuildArgs(string rawCommand)
    {
        // 使用 -Command 參數執行指令
        return new[] { "-Command", rawCommand };
    }
}