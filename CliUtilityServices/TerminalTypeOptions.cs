using System.ComponentModel;

namespace CliUtilityServices;

/// <summary>
/// Defines supported command shell implementations.
/// </summary>
public enum TerminalTypeOptions
{
    /// <summary>
    /// Windows Command Prompt.
    /// </summary>
    [Description("Windows Command Prompt (cmd.exe)")]
    Cmd = 0,

    /// <summary>
    /// Windows PowerShell, typically version 5.1 or earlier.
    /// </summary>
    [Description("Windows PowerShell (powershell.exe, Windows only)")]
    PowerShell = 1,

    /// <summary>
    /// Cross-platform PowerShell using pwsh.
    /// </summary>
    [Description("PowerShell (pwsh, cross-platform)")]
    PowerShellCore = 2,

    /// <summary>
    /// Bash shell.
    /// </summary>
    [Description("Bash shell")]
    Bash = 3,

    /// <summary>
    /// Zsh shell.
    /// </summary>
    [Description("Zsh shell")]
    Zsh = 4,
}