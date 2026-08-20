using System.ComponentModel;

namespace CliUtilityServices;

public enum TerminalTypeOptions
{
    [Description("DOS Command (only supported in Windows)")]
    Cmd,

    [Description("PowerShell with version <= 4 (only supported in Windows)")]
    PowerShell,

    [Description("PowerShell Core (i.e. PowerShell 5+) (only supported in Windows)")]
    PowerShellCore,

    [Description("Bash (usually used in Linux)")]
    Bash,

    [Description("Zsh (used in macOs Catalina 10.15+ )")]
    Zsh,

}
