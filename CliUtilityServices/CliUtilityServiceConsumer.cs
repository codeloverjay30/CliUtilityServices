using CliWrap;
using Commands.Infrastructure;

namespace CliUtilityServices;

public class CliUtilityServiceConsumer: ICliUtilityServiceConsumer
{
    private readonly ICliCommandExecutor _commandExecutor;

    public CliUtilityServiceConsumer(
        ICliCommandExecutor commandExecutor
    )
    {
        // 嚴格防禦依賴注入為空
        ArgumentNullException.ThrowIfNull(commandExecutor,nameof(commandExecutor));
        
        _commandExecutor = commandExecutor;
    }

    public async Task<CommandExecutionResult> RunCommandSafelyAsync(
        string commandText,
        IEnumerable<string> arguments
    )
    {
        try
        {
            // 使用內建的建構器建立防禦性輸入參數（自動套用平台編碼與預設管線策略）[cite: 1]
            var commandLineInput = new CommandLineInputBuilder()
                .WithCommand(commandText)
                .WithArguments(arguments)
                .WithValidation(CommandResultValidation.ZeroExitCode)
                .Build();

            // 執行自動偵測作業系統（Windows 自動切換 Cmd/PowerShell，macOS/Linux 自動切換 Bash/Zsh）[cite: 1]
            var result = await _commandExecutor.ExecuteAutoDetectedAsync(commandLineInput);

            return result;
        }
        catch (NotSupportedException ex)
        {
            // 攔截不支援的終端機型態或平台例外
            throw new InvalidOperationException($"不支援的命令執行環境: {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            // 防禦性封裝底層 IO 或處理過程中的異常
            throw new InvalidOperationException($"透過 CliUtilityServices 執行命令失敗: {ex.Message}", ex);
        }
    }
}
