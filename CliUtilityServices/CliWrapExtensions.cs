using System.Text;
using CliWrap;
using CliWrap.Buffered;

namespace CliUtilityServices;

public static class CliWrapExtensions
{
    public static async Task<BufferedCommandResult> ExecuteWithEncodingAsync(
        this Command command, Encoding encoding)
    {
        var stdOut = new StringBuilder();
        var stdErr = new StringBuilder();

        var result = await command
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOut, encoding))
            .WithStandardErrorPipe(PipeTarget.ToStringBuilder(stdErr, encoding))
            .ExecuteAsync();

        return new BufferedCommandResult(
            result.ExitCode, 
            result.StartTime, 
            result.ExitTime, 
            stdOut.ToString(), 
            stdErr.ToString()
        );
    }
}
