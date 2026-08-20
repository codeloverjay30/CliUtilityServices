using System.IO.Abstractions;
using System.Text;
using EnvironmentUtilityServices;

namespace CliUtilityServices.Terminals;

/// <summary>
/// Provides the terminal configuration and argument builder for Windows PowerShell (v5.1 and below).
/// </summary>
public class PowerShellProvider : ITerminalProvider
{
    private readonly IFileSystem _fileSystem;

    /// <inheritdoc />
    public string TerminalName => "powershell";

    /// <inheritdoc />
    public TerminalTypeOptions TerminalType => TerminalTypeOptions.PowerShell;

    /// <inheritdoc />
    public Encoding DefaultEncoding => Encoding.UTF8;

    /// <summary>
    /// Initializes a new instance of the <see cref="PowerShellProvider"/> class.
    /// </summary>
    /// <param name="fileSystem">The abstraction of the file system.</param>
    /// <exception cref="ArgumentNullException">Thrown when fileSystem is null.</exception>
    public PowerShellProvider(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;
    }
    
    /// <inheritdoc />
    public string GetExecutablePath(IEnvironmentService environmentService)
    {
        ArgumentNullException.ThrowIfNull(environmentService);
        
        if (!environmentService.IsWindows())
        {
            throw new PlatformNotSupportedException("Legacy Windows PowerShell is only supported on Windows.");
        }

        return "powershell.exe";
    }

    /// <inheritdoc />
    public IEnumerable<string> BuildArgs(string rawCommand)
    {
        return new[] { "-NoProfile", "-NonInteractive", "-Command", rawCommand };
    }
}