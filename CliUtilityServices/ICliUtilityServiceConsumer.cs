using Commands.Infrastructure;

namespace CliUtilityServices;

public interface ICliUtilityServiceConsumer
{
    Task<CommandExecutionResult> RunCommandSafelyAsync(
        string commandText,
        IEnumerable<string> arguments
    );
}
