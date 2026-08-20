using System.Text;
using EnvironmentUtilityServices;

namespace CliUtilityServices.Terminals;

public interface ITerminalProvider
{
    string TerminalName { get; }
    TerminalTypeOptions TerminalType { get; }
    string GetExecutablePath(IEnvironmentService environmentService);
    IEnumerable<string> BuildArgs(string rawCommand);
    Encoding DefaultEncoding { get; }
}
