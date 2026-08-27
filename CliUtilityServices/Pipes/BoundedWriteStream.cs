namespace CliUtilityServices.Pipes;

/// <summary>
/// Provides a write-only stream decorator that prevents the underlying
/// stream from receiving more than a configured number of bytes.
/// </summary>
/// <remarks>
/// The limit is enforced before each write reaches the underlying stream.
/// A write that would exceed the configured quota is rejected in its entirety.
/// </remarks>
internal sealed class BoundedWriteStream : Stream
{
    private readonly Stream _innerStream;
    private readonly long _maximumBytes;
    private readonly string _streamName;

    private long _bytesWritten;
    private int _isDisposed;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="BoundedWriteStream"/> class.
    /// </summary>
    /// <param name="innerStream">
    /// The underlying destination stream.
    /// </param>
    /// <param name="maximumBytes">
    /// The maximum number of bytes that may be written.
    /// </param>
    /// <param name="streamName">
    /// The logical stream name used for diagnostics.
    /// </param>
    public BoundedWriteStream(
        Stream innerStream,
        long maximumBytes,
        string streamName)
    {
        ArgumentNullException.ThrowIfNull(
            innerStream,
            nameof(innerStream));

        ArgumentException.ThrowIfNullOrWhiteSpace(
            streamName,
            nameof(streamName));

        if (!innerStream.CanWrite)
        {
            throw new ArgumentException(
                "The underlying stream must be writable.",
                nameof(innerStream));
        }

        if (maximumBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumBytes),
                maximumBytes,
                "Maximum output bytes must be greater than zero.");
        }

        _innerStream = innerStream;
        _maximumBytes = maximumBytes;
        _streamName = streamName;
    }

    /// <summary>
    /// Gets the number of bytes successfully written through this stream.
    /// </summary>
    public long BytesWritten =>
        Interlocked.Read(ref _bytesWritten);

    /// <inheritdoc />
    public override bool CanRead => false;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite =>
        Volatile.Read(ref _isDisposed) == 0
        && _innerStream.CanWrite;

    /// <inheritdoc />
    public override long Length =>
        throw new NotSupportedException(
            "Length is not supported by this bounded write stream.");

    /// <inheritdoc />
    public override long Position
    {
        get => throw new NotSupportedException(
            "Position is not supported by this bounded write stream.");

        set => throw new NotSupportedException(
            "Position is not supported by this bounded write stream.");
    }

    /// <inheritdoc />
    public override void Flush()
    {
        ThrowIfDisposed();

        _innerStream.Flush();
    }

    /// <inheritdoc />
    public override Task FlushAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        return _innerStream.FlushAsync(
            cancellationToken);
    }

    /// <inheritdoc />
    public override void Write(
        byte[] buffer,
        int offset,
        int count)
    {
        ArgumentNullException.ThrowIfNull(
            buffer,
            nameof(buffer));

        ArgumentOutOfRangeException.ThrowIfNegative(
            offset,
            nameof(offset));

        ArgumentOutOfRangeException.ThrowIfNegative(
            count,
            nameof(count));

        if (offset > buffer.Length - count)
        {
            throw new ArgumentException(
                "Offset and count exceed the buffer bounds.");
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
        ThrowIfDisposed();

        ReserveBytes(buffer.Length);

        try
        {
            _innerStream.Write(buffer);
        }
        catch
        {
            ReleaseReservedBytes(
                buffer.Length);

            throw;
        }
    }

    /// <inheritdoc />
    public override async Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(
            buffer,
            nameof(buffer));

        ArgumentOutOfRangeException.ThrowIfNegative(
            offset,
            nameof(offset));

        ArgumentOutOfRangeException.ThrowIfNegative(
            count,
            nameof(count));

        if (offset > buffer.Length - count)
        {
            throw new ArgumentException(
                "Offset and count exceed the buffer bounds.");
        }

        await WriteAsync(
            buffer.AsMemory(
                offset,
                count),
            cancellationToken);
    }

    /// <inheritdoc />
    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ReserveBytes(buffer.Length);

        try
        {
            await _innerStream.WriteAsync(
                buffer,
                cancellationToken);
        }
        catch
        {
            ReleaseReservedBytes(
                buffer.Length);

            throw;
        }
    }

    /// <inheritdoc />
    public override int Read(
        byte[] buffer,
        int offset,
        int count)
    {
        throw new NotSupportedException(
            "Reading is not supported by this bounded write stream.");
    }

    /// <inheritdoc />
    public override long Seek(
        long offset,
        SeekOrigin origin)
    {
        throw new NotSupportedException(
            "Seeking is not supported by this bounded write stream.");
    }

    /// <inheritdoc />
    public override void SetLength(
        long value)
    {
        throw new NotSupportedException(
            "SetLength is not supported by this bounded write stream.");
    }

    /// <summary>
    /// Reserves output bytes before they are written to the underlying stream.
    /// </summary>
    /// <param name="count">
    /// The number of bytes requested by the current write.
    /// </param>
    /// <exception cref="OutputLimitExceededException">
    /// Thrown when the requested write would exceed the configured quota.
    /// </exception>
    private void ReserveBytes(
        int count)
    {
        if (count == 0)
        {
            return;
        }

        while (true)
        {
            long current =
                Interlocked.Read(
                    ref _bytesWritten);

            if (count > _maximumBytes - current)
            {
                long attempted;

                try
                {
                    attempted =
                        checked(current + count);
                }
                catch (OverflowException)
                {
                    attempted =
                        long.MaxValue;
                }

                throw new OutputLimitExceededException(
                    _streamName,
                    _maximumBytes,
                    attempted);
            }

            long next =
                current + count;

            if (Interlocked.CompareExchange(
                    ref _bytesWritten,
                    next,
                    current) == current)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Releases a previous reservation when the underlying write fails.
    /// </summary>
    /// <param name="count">
    /// The number of bytes that were reserved.
    /// </param>
    private void ReleaseReservedBytes(
        int count)
    {
        if (count == 0)
        {
            return;
        }

        Interlocked.Add(
            ref _bytesWritten,
            -count);
    }

    /// <summary>
    /// Throws when this stream has already been disposed.
    /// </summary>
    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _isDisposed) != 0,
            this);
    }

    /// <inheritdoc />
    protected override void Dispose(
        bool disposing)
    {
        if (Interlocked.Exchange(
                ref _isDisposed,
                1) != 0)
        {
            return;
        }

        if (disposing)
        {
            _innerStream.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(
                ref _isDisposed,
                1) != 0)
        {
            return;
        }

        await _innerStream
            .DisposeAsync()
            .ConfigureAwait(false);

        GC.SuppressFinalize(this);
    }
}