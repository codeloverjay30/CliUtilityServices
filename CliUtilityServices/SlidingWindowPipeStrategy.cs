using System.Text;
using CliWrap;

namespace CliUtilityServices.Pipes;

/// <summary>
/// Implements a sliding window pipe strategy that limits memory usage by only keeping the last N lines.
/// </summary>
public class SlidingWindowPipeStrategy : ICommandPipeStrategy
{
    private readonly int _maxLines;
    private readonly Queue<string> _outLines;
    private readonly Queue<string> _errLines;
    private readonly object _lockObj = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SlidingWindowPipeStrategy"/> class.
    /// </summary>
    /// <param name="maxLines">The maximum number of lines to retain in memory.</param>
    public SlidingWindowPipeStrategy(int maxLines = 500)
    {
        _maxLines = maxLines;
        _outLines = new Queue<string>(maxLines);
        _errLines = new Queue<string>(maxLines);
    }

    /// <inheritdoc />
    public Command ConfigurePipes(Command command, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(encoding);

        var outPipe = PipeTarget.ToDelegate(line => 
        {
            lock (_lockObj)
            {
                if (_outLines.Count >= _maxLines) _outLines.Dequeue();
                _outLines.Enqueue(line);
            }
        }, encoding);

        var errPipe = PipeTarget.ToDelegate(line => 
        {
            lock (_lockObj)
            {
                if (_errLines.Count >= _maxLines) _errLines.Dequeue();
                _errLines.Enqueue(line);
            }
        }, encoding);

        return command.WithStandardOutputPipe(outPipe).WithStandardErrorPipe(errPipe);
    }

    /// <inheritdoc />
    public Task<(string StandardOutput, string StandardError)> GetResultAsync()
    {
        var sbOut = new StringBuilder();
        var sbErr = new StringBuilder();

        lock (_lockObj)
        {
            if (_outLines.Count == _maxLines) sbOut.AppendLine("[... Outputs truncated for memory defense ...]");
            foreach (var line in _outLines) sbOut.AppendLine(line);
            foreach (var line in _errLines) sbErr.AppendLine(line);
        }

        return Task.FromResult((sbOut.ToString(), sbErr.ToString()));
    }
}