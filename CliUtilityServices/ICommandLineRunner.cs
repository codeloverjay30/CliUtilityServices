using System.ComponentModel;
using System.Diagnostics;
using CliUtilityServices.Terminals;
using CliWrap;
using CliWrap.Buffered;
using CustomDataAnnotations.Maintenance;

namespace CliUtilityServices;

/// <summary>
/// The contract of running commands using terminal
/// </summary>
/// <remarks>
/// It violates SRP as its methods return <see cref="global::CliWrap.Buffered.BufferedCommandResult"/> 
/// which is tigt-coupled with <see cref="global::CliWrap"/> package.
/// Use <see cref="global::CliUtilityServices.ICliCommandExecutor"/> 
/// which its methods return <see cref="global::Commands.Infrastructure.CommandExecutionResult"/>
/// </remarks>
[TechnicalDebt(CategoryType.CodeSmell | CategoryType.LowRigidityIssue | CategoryType.ViolateSrpIssue, "ICliCommandExecutor")]
[Obsolete("""
It violates SRP as its methods return <see cref="global::CliWrap.Buffered.BufferedCommandResult"/> 
which is tigt-coupled with <see cref="global::CliWrap"/> package.
Use <see cref="global::CliUtilityServices.ICliCommandExecutor"/> 
which its methods return <see cref="global::Commands.Infrastructure.CommandExecutionResult"/>
""")]
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ICommandLineRunner
{
    
    IEnumerable<ITerminalProvider> SupportedTerminalProviders { get; }
    
    Task<BufferedCommandResult> ExecuteInShellAsync(
        TerminalTypeOptions terminalType,
        CommandLineInput commandLineInput
    );

    Task<BufferedCommandResult> ExecuteInShellAsync(
        TerminalTypeOptions terminalType,
        string rawCommand,
        string workingDirectory = ""
    );

    Task<BufferedCommandResult> ExecuteAutoDetectedAsync(
        CommandLineInput commandLineInput
    );

    /// <inheritdoc cref="global::CliUtilityServices.CliWrapRunner.ExecuteAsync(CommandLineInput)"/>
    [Obsolete("This method is unsafe and smells bad, consider use global::CliUtilityServices.ICommandLineRunner.ExecuteInShellAsync(TerminalTypeOptions, CommandLineInput)")]
    [TechnicalDebt(CategoryType.SecurityVulnerability | CategoryType.CodeSmell | CategoryType.OutdatedStrategy, "global::CliUtilityServices.ICommandLineRunner.ExecuteInShellAsync(TerminalTypeOptions, CommandLineInput)")]
    Task<BufferedCommandResult> ExecuteAsync(CommandLineInput commandLineInput);
}