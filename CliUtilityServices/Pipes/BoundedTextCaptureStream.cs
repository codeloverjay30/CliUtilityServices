using System.Buffers;
using System.Text;

namespace CliUtilityServices.Pipes;

/// <summary>
/// Provides a write-only stream that incrementally decodes command output
/// while bounding the amount of memory retained for the current line.
/// </summary>
/// <remarks>
/// The stream retains only the newest characters of an unterminated line.
/// This prevents a child process from causing unbounded memory growth by
/// emitting an excessively large line without a newline.
/// </remarks>
internal sealed class BoundedTextCaptureStream : Stream
{
    private const int DecodeBufferSize = 4096;

    private readonly Decoder _decoder;
    private readonly SlidingWindowTextBuffer _destination;
    private readonly char[] _currentLineBuffer;
    private readonly object _syncRoot = new();
    private bool _pendingCarriageReturn;

    private int _currentLineStart;
    private int _currentLineCount;

    private bool _currentLineWasTruncated;
    private bool _isCompleted;
    private bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="BoundedTextCaptureStream"/> class.
    /// </summary>
    /// <param name="encoding">
    /// The encoding used to decode incoming bytes.
    /// </param>
    /// <param name="destination">
    /// The destination sliding-window text buffer.
    /// </param>
    /// <param name="maxCurrentLineCharacters">
    /// The maximum number of characters retained for an incomplete line.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="encoding"/> or
    /// <paramref name="destination"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxCurrentLineCharacters"/> is not positive.
    /// </exception>
    public BoundedTextCaptureStream(
        Encoding encoding,
        SlidingWindowTextBuffer destination,
        int maxCurrentLineCharacters)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        ArgumentNullException.ThrowIfNull(destination);

        if (maxCurrentLineCharacters <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxCurrentLineCharacters),
                maxCurrentLineCharacters,
                "Maximum current line characters must be greater than zero.");
        }

        _decoder = encoding.GetDecoder();
        _destination = destination;

        _currentLineBuffer =
            new char[maxCurrentLineCharacters];
    }

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite
    {
        get
        {
            lock (_syncRoot)
            {
                return !_isDisposed
                    && !_isCompleted;
            }
        }
    }


    /// <inheritdoc />
    public override long Length =>
        throw new NotSupportedException(
            "Length is not supported by this text capture stream.");

    /// <inheritdoc />
    public override long Position
    {
        get =>
            throw new NotSupportedException(
                "Position is not supported by this text capture stream.");

        set =>
            throw new NotSupportedException(
                "Position is not supported by this text capture stream.");
    }

    /// <inheritdoc />
    public override void Flush()
    {
        lock (_syncRoot)
        {
            ThrowIfUnavailable();
        }
    }

    /// <inheritdoc />
    public override Task FlushAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Flush();

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void Write(
        byte[] buffer,
        int offset,
        int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);

        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        if (offset > buffer.Length - count)
        {
            throw new ArgumentException(
                "Offset and count must identify a valid range within the buffer.");
        }

        Write(
            buffer.AsSpan(
                offset,
                count));
    }

    /// <inheritdoc />
    public override void Write(
        ReadOnlySpan<byte> buffer)
    {
        lock (_syncRoot)
        {
            ThrowIfUnavailable();

            Decode(
                buffer,
                flush: false);
        }
    }

    /// <inheritdoc />
    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Write(
            buffer,
            offset,
            count);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Write(buffer.Span);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Completes decoding and commits the final unterminated line, if present.
    /// </summary>
    /// <remarks>
    /// Calling this method more than once is safe. After completion, additional
    /// writes are rejected.
    /// </remarks>
    public void Complete()
    {
        lock (_syncRoot)
        {
            ThrowIfDisposed();

            if (_isCompleted)
            {
                return;
            }

            Decode(
                ReadOnlySpan<byte>.Empty,
                flush: true);

            CommitFinalLine();

            _isCompleted = true;
        }
    }

    /// <inheritdoc />
    public override int Read(
        byte[] buffer,
        int offset,
        int count)
    {
        throw new NotSupportedException(
            "Reading is not supported by this text capture stream.");
    }

    /// <inheritdoc />
    public override long Seek(
        long offset,
        SeekOrigin origin)
    {
        throw new NotSupportedException(
            "Seeking is not supported by this text capture stream.");
    }

    /// <inheritdoc />
    public override void SetLength(
        long value)
    {
        throw new NotSupportedException(
            "Setting length is not supported by this text capture stream.");
    }

    /// <inheritdoc />
    protected override void Dispose(
        bool disposing)
    {
        if (!disposing)
        {
            base.Dispose(disposing);
            return;
        }

        lock (_syncRoot)
        {
            if (_isDisposed)
            {
                base.Dispose(disposing);
                return;
            }

            try
            {
                if (!_isCompleted)
                {
                    Decode(
                        ReadOnlySpan<byte>.Empty,
                        flush: true);

                    CommitFinalLine();

                    _isCompleted = true;
                }
            }
            finally
            {
                _isDisposed = true;
            }
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Incrementally decodes bytes without materializing the entire command
    /// output or an unbounded line.
    /// </summary>
    /// <param name="bytes">
    /// The bytes to decode.
    /// </param>
    /// <param name="flush">
    /// Indicates whether no additional input will be supplied to the decoder.
    /// </param>
    private void Decode(
        ReadOnlySpan<byte> bytes,
        bool flush)
    {
        char[] rentedBuffer =
            ArrayPool<char>.Shared.Rent(
                DecodeBufferSize);

        try
        {
            Span<char> characters =
                rentedBuffer.AsSpan(
                    0,
                    DecodeBufferSize);

            while (true)
            {
                _decoder.Convert(
                    bytes,
                    characters,
                    flush,
                    out int bytesUsed,
                    out int charsUsed,
                    out bool completed);

                if (charsUsed > 0)
                {
                    ProcessCharacters(
                        characters[..charsUsed]);
                }

                bytes =
                    bytes[bytesUsed..];

                if (completed)
                {
                    return;
                }

                if (bytesUsed == 0
                    && charsUsed == 0)
                {
                    throw new InvalidOperationException(
                        "The output decoder made no progress while decoding command output.");
                }
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(
                rentedBuffer,
                clearArray: true);
        }
    }

    /// <summary>
    /// Processes decoded characters and normalizes line boundaries.
    /// </summary>
    /// <param name="characters">The decoded characters to process.</param>
    private void ProcessCharacters(
        ReadOnlySpan<char> characters)
    {
        foreach (char character in characters)
        {
            if (_pendingCarriageReturn)
            {
                _pendingCarriageReturn = false;

                if (character == '\n')
                {
                    CommitCompletedLine();
                    continue;
                }

                AppendCurrentLineCharacter('\r');
            }

            if (character == '\r')
            {
                _pendingCarriageReturn = true;
                continue;
            }

            if (character == '\n')
            {
                CommitCompletedLine();
                continue;
            }

            AppendCurrentLineCharacter(character);
        }
    }


    /// <summary>
    /// Appends a character to the bounded current-line circular buffer.
    /// </summary>
    /// <param name="character">
    /// The character to append.
    /// </param>
    private void AppendCurrentLineCharacter(
        char character)
    {
        if (_currentLineCount
            < _currentLineBuffer.Length)
        {
            int writeIndex =
                GetCircularBufferIndex(_currentLineCount);

            _currentLineBuffer[writeIndex] =
                character;

            _currentLineCount =
                checked(
                    _currentLineCount + 1);


            return;
        }

        _currentLineBuffer[_currentLineStart] =
            character;

        _currentLineStart =
            (_currentLineStart + 1)
            % _currentLineBuffer.Length;

        _currentLineWasTruncated = true;
    }

    /// <summary>
    /// Calculates a physical circular-buffer index from a logical offset
    /// without relying on potentially overflowing integer addition.
    /// </summary>
    /// <param name="offset">
    /// The logical offset from the current buffer start.
    /// </param>
    /// <returns>
    /// The corresponding physical buffer index.
    /// </returns>
    private int GetCircularBufferIndex(
        int offset)
    {
        if ((uint)offset
            >= (uint)_currentLineBuffer.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(offset),
                offset,
                "The circular-buffer offset must be within the buffer capacity.");
        }

        int remaining =
            _currentLineBuffer.Length
            - _currentLineStart;

        return offset < remaining
            ? _currentLineStart + offset
            : offset - remaining;
    }


    /// <summary>
    /// Commits a newline-terminated line to the destination buffer.
    /// </summary>
    private void CommitCompletedLine()
    {
        string line =
            CreateCurrentLine(
                trimTrailingCarriageReturn: false);

        _destination.AddLine(
            line,
            _currentLineWasTruncated);

        ResetCurrentLine();
    }


    /// <summary>
    /// Commits the final unterminated line when retained content exists.
    /// </summary>
    private void CommitFinalLine()
    {
        if (_pendingCarriageReturn)
        {
            _pendingCarriageReturn = false;
            AppendCurrentLineCharacter('\r');
        }

        if (_currentLineCount == 0)
        {
            if (_currentLineWasTruncated)
            {
                _destination.MarkTruncated();
            }

            ResetCurrentLine();
            return;
        }

        string line =
            CreateCurrentLine(
                trimTrailingCarriageReturn: false);

        _destination.AddLine(
            line,
            _currentLineWasTruncated);

        ResetCurrentLine();
    }


    /// <summary>
    /// Creates an immutable string from the bounded current-line buffer.
    /// </summary>
    /// <param name="trimTrailingCarriageReturn">
    /// Indicates whether a trailing carriage return should be excluded.
    /// </param>
    /// <returns>
    /// The retained current-line content.
    /// </returns>
    private string CreateCurrentLine(
        bool trimTrailingCarriageReturn)
    {
        int characterCount =
            _currentLineCount;

        if (trimTrailingCarriageReturn)
        {
            characterCount--;
        }

        if (characterCount <= 0)
        {
            return string.Empty;
        }

        return string.Create(
            characterCount,
            this,
            static (
                destination,
                stream) =>
            {
                stream.CopyCurrentLineTo(
                    destination);
            });
    }

    /// <summary>
    /// Copies the current-line contents into the supplied destination span.
    /// </summary>
    /// <param name="destination">
    /// The destination span.
    /// </param>
    private void CopyCurrentLineTo(
        Span<char> destination)
    {
        if (destination.Length == 0)
        {
            return;
        }

        int firstSegmentLength =
            Math.Min(
                destination.Length,
                _currentLineBuffer.Length
                - _currentLineStart);

        _currentLineBuffer
            .AsSpan(
                _currentLineStart,
                firstSegmentLength)
            .CopyTo(destination);

        int remaining =
            destination.Length
            - firstSegmentLength;

        if (remaining > 0)
        {
            _currentLineBuffer
                .AsSpan(
                    0,
                    remaining)
                .CopyTo(
                    destination[
                        firstSegmentLength..]);
        }
    }

    /// <summary>
    /// Gets the newest character currently retained in the line buffer.
    /// </summary>
    /// <returns>
    /// The newest retained character.
    /// </returns>
    private char GetLastCurrentLineCharacter()
    {
        if (_currentLineCount <= 0)
        {
            throw new InvalidOperationException(
                "The current-line buffer is empty.");
        }

        int index =
            (_currentLineStart
             + _currentLineCount
             - 1)
            % _currentLineBuffer.Length;

        return _currentLineBuffer[index];
    }

    /// <summary>
    /// Resets current-line state without reallocating the circular buffer.
    /// </summary>
    private void ResetCurrentLine()
    {
        _currentLineStart = 0;
        _currentLineCount = 0;
        _currentLineWasTruncated = false;
    }

    /// <summary>
    /// Throws when the stream cannot accept additional writes.
    /// </summary>
    private void ThrowIfUnavailable()
    {
        ThrowIfDisposed();

        if (_isCompleted)
        {
            throw new InvalidOperationException(
                "The text capture stream has already been completed.");
        }
    }

    /// <summary>
    /// Throws when the stream has already been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            _isDisposed,
            this);
    }
}