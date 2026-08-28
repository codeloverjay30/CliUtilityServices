using System.Text;
using CliWrap;

namespace CliUtilityServices.Pipes;

/// <summary>
/// Configures bounded stream-based capture for command standard output
/// and standard error.
/// </summary>
/// <remarks>
/// Each instance represents a single command execution lifecycle.
/// The strategy must not be configured for more than one execution.
/// </remarks>
public sealed class SlidingWindowPipeStrategy : ICommandPipeStrategy
{
    /// <summary>
    /// Gets the default maximum number of retained lines for each output stream.
    /// </summary>
    public const int DefaultMaxLines = 500;

    /// <summary>
    /// Gets the default maximum number of retained characters for each output stream.
    /// </summary>
    public const int DefaultMaxRetainedCharacters = 1_048_576;

    private readonly int _maxLines;
    private readonly int _maxRetainedCharacters;
    private readonly object _syncRoot = new();

    private SlidingWindowTextBuffer? _standardOutputBuffer;
    private SlidingWindowTextBuffer? _standardErrorBuffer;

    private BoundedTextCaptureStream? _standardOutputStream;
    private BoundedTextCaptureStream? _standardErrorStream;

    private bool _isConfigured;
    private bool _isCompleted;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="SlidingWindowPipeStrategy"/> class.
    /// </summary>
    /// <param name="maxLines">
    /// The maximum number of retained lines for each output stream.
    /// </param>
    /// <param name="maxRetainedCharacters">
    /// The maximum number of retained characters for each output stream.
    /// This limit also bounds the retained characters of an incomplete line.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when either configured limit is less than or equal to zero.
    /// </exception>
    public SlidingWindowPipeStrategy(
        int maxLines = DefaultMaxLines,
        int maxRetainedCharacters = DefaultMaxRetainedCharacters)
    {
        if (maxLines <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxLines),
                maxLines,
                "Maximum retained lines must be greater than zero.");
        }

        if (maxRetainedCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxRetainedCharacters),
                maxRetainedCharacters,
                "Maximum retained characters must be greater than zero.");
        }

        _maxLines = maxLines;
        _maxRetainedCharacters = maxRetainedCharacters;
    }

    /// <summary>
    /// Configures bounded standard-output and standard-error pipes for the command.
    /// </summary>
    /// <param name="command">
    /// The command to configure.
    /// </param>
    /// <param name="encoding">
    /// The encoding used to incrementally decode command output.
    /// </param>
    /// <returns>
    /// A command configured with bounded stream-based output pipes.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="command"/> or
    /// <paramref name="encoding"/> is null.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when this strategy instance has already been configured.
    /// </exception>
    public Command ConfigurePipes(
        Command command,
        Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(encoding);

        lock (_syncRoot)
        {
            if (_isConfigured)
            {
                throw new InvalidOperationException(
                    "The sliding window pipe strategy has already been configured.");
            }

            var standardOutputBuffer =
                new SlidingWindowTextBuffer(
                    _maxLines,
                    _maxRetainedCharacters);

            var standardErrorBuffer =
                new SlidingWindowTextBuffer(
                    _maxLines,
                    _maxRetainedCharacters);

            BoundedTextCaptureStream? standardOutputStream = null;
            BoundedTextCaptureStream? standardErrorStream = null;

            try
            {
                standardOutputStream =
                    new BoundedTextCaptureStream(
                        encoding,
                        standardOutputBuffer,
                        _maxRetainedCharacters);

                standardErrorStream =
                    new BoundedTextCaptureStream(
                        encoding,
                        standardErrorBuffer,
                        _maxRetainedCharacters);

                Command configuredCommand =
                    command
                        .WithStandardOutputPipe(
                            PipeTarget.ToStream(
                                standardOutputStream))
                        .WithStandardErrorPipe(
                            PipeTarget.ToStream(
                                standardErrorStream));

                /*
                 * Publish the newly created state only after the entire
                 * configuration operation has completed successfully.
                 *
                 * This prevents a partially configured strategy from becoming
                 * externally observable if command configuration fails.
                 */
                _standardOutputBuffer =
                    standardOutputBuffer;

                _standardErrorBuffer =
                    standardErrorBuffer;

                _standardOutputStream =
                    standardOutputStream;

                _standardErrorStream =
                    standardErrorStream;

                _isConfigured = true;

                return configuredCommand;
            }
            catch
            {
                /*
                 * The temporary streams are owned by this configuration
                 * attempt until their state has been published successfully.
                 */
                standardOutputStream?.Dispose();
                standardErrorStream?.Dispose();

                throw;
            }
        }
    }

    /// <summary>
    /// Completes output decoding and returns snapshots of the retained
    /// standard output and standard error.
    /// </summary>
    /// <returns>
    /// A task containing the retained standard output and standard error.
    /// </returns>
    /// <remarks>
    /// The first invocation completes both capture streams. Subsequent
    /// invocations return snapshots of the already completed buffers.
    /// This method must be called only after command execution has finished.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the strategy has not been configured.
    /// </exception>
    public Task<(
        string StandardOutput,
        string StandardError)> GetResultAsync()
    {
        lock (_syncRoot)
        {
            EnsureConfigured();

            if (!_isCompleted)
            {
                CompleteCaptureStreams();

                _isCompleted = true;
            }

            string standardOutput =
                _standardOutputBuffer!.GetSnapshot();

            string standardError =
                _standardErrorBuffer!.GetSnapshot();

            return Task.FromResult(
                (
                    StandardOutput: standardOutput,
                    StandardError: standardError
                ));
        }
    }

    /// <summary>
    /// Completes both output capture streams.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when either capture stream is unexpectedly unavailable.
    /// </exception>
    private void CompleteCaptureStreams()
    {
        if (_standardOutputStream is null
            || _standardErrorStream is null)
        {
            throw new InvalidOperationException(
                "The sliding window capture streams are unavailable.");
        }

        Exception? standardOutputException = null;

        try
        {
            _standardOutputStream.Complete();
        }
        catch (Exception exception)
        {
            standardOutputException = exception;
        }

        try
        {
            _standardErrorStream.Complete();
        }
        catch when (standardOutputException is not null)
        {
            /*
             * Preserve the first completion failure while still attempting
             * to finalize the second independent capture stream.
             */
        }

        if (standardOutputException is not null)
        {
            throw standardOutputException;
        }
    }

    /// <summary>
    /// Verifies that the strategy has been configured successfully.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the strategy has not been configured or its internal
    /// configuration state is inconsistent.
    /// </exception>
    private void EnsureConfigured()
    {
        if (!_isConfigured)
        {
            throw new InvalidOperationException(
                "The sliding window pipe strategy has not been configured.");
        }

        if (_standardOutputBuffer is null
            || _standardErrorBuffer is null
            || _standardOutputStream is null
            || _standardErrorStream is null)
        {
            throw new InvalidOperationException(
                "The sliding window pipe strategy is in an inconsistent configured state.");
        }
    }
}