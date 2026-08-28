namespace CliUtilityServices.Pipes;

/// <summary>
/// Provides a write-only stream decorator that prevents the underlying
/// stream from receiving more than a configured number of bytes.
/// </summary>
/// <remarks>
/// The configured quota is reserved before each write reaches the underlying
/// stream. Once reserved, quota is not released when the underlying write
/// fails because a stream may partially write data before throwing.
/// This fail-closed behavior prevents the effective output from exceeding
/// the configured quota.
/// </remarks>
internal sealed class BoundedWriteStream : Stream
{
    private readonly Stream _innerStream;
    private readonly long _maximumBytes;
    private readonly string _streamName;

    private long _consumedQuotaBytes;
    private int _isDisposed;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="BoundedWriteStream"/> class.
    /// </summary>
    /// <param name="innerStream">
    /// The underlying destination stream.
    /// </param>
    /// <param name="maximumBytes">
    /// The maximum number of bytes that may be reserved for writes.
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
    /// Gets the number of bytes consumed from the configured write quota.
    /// </summary>
    /// <remarks>
    /// This value represents reserved quota rather than a guaranteed count
    /// of bytes successfully persisted by the underlying stream.
    /// </remarks>
    public long ConsumedQuotaBytes =>
        Interlocked.Read(
            ref _consumedQuotaBytes);

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

        ReserveQuota(
            buffer.Length);

        _innerStream.Write(
            buffer);
    }

    /// <inheritdoc />
    public override Task WriteAsync(
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

        return WriteAsync(
                buffer.AsMemory(
                    offset,
                    count),
                cancellationToken)
            .AsTask();
    }

    /// <inheritdoc />
    public override async ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        ReserveQuota(
            buffer.Length);

        await _innerStream
            .WriteAsync(
                buffer,
                cancellationToken)
            .ConfigureAwait(false);
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
    /// Atomically reserves quota before a write reaches the underlying stream.
    /// </summary>
    /// <param name="count">
    /// The number of bytes requested by the current write.
    /// </param>
    /// <exception cref="OutputLimitExceededException">
    /// Thrown when the requested reservation would exceed the configured quota.
    /// </exception>
    private void ReserveQuota(
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
                    ref _consumedQuotaBytes);

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
                    ref _consumedQuotaBytes,
                    next,
                    current) == current)
            {
                return;
            }
        }
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

        base.Dispose(
            disposing);
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