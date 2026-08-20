using CliWrap.Buffered;
using Commands.Infrastructure;

namespace CommandResult.Infrastructure;

/// <inheritdoc/>
public class CliResultProcessor : ICliResultProcessor
{
    /// <inheritdoc/>
    public CommandExecutionResult Process(BufferedCommandResult bufferedCommandResult)
    {
        // 可以在此增加效能優化，例如針對大輸出進行額外的 Span 處理
        return new CommandExecutionResult(
            StandardOutput: bufferedCommandResult.StandardOutput,
            StandardError: bufferedCommandResult.StandardError,
            ExitCode: bufferedCommandResult.ExitCode,
            RunTime: bufferedCommandResult.RunTime
        );
    }
}