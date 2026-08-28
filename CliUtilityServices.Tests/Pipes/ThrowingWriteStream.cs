internal sealed class ThrowingWriteStream
    : Stream
{
    private readonly Exception _writeException;

    public ThrowingWriteStream(
        Exception writeException)
    {
        ArgumentNullException.ThrowIfNull(
            writeException);

        _writeException =
            writeException;
    }

    public override bool CanRead => false;

    public override bool CanSeek => false;

    public override bool CanWrite => true;

    public override long Length =>
        throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override Task FlushAsync(
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public override int Read(
        byte[] buffer,
        int offset,
        int count)
    {
        throw new NotSupportedException();
    }

    public override long Seek(
        long offset,
        SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(
        long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(
        byte[] buffer,
        int offset,
        int count)
    {
        throw _writeException;
    }

    public override void Write(
        ReadOnlySpan<byte> buffer)
    {
        throw _writeException;
    }

    public override Task WriteAsync(
        byte[] buffer,
        int offset,
        int count,
        CancellationToken cancellationToken)
    {
        return Task.FromException(
            _writeException);
    }

    public override ValueTask WriteAsync(
        ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromException(
            _writeException);
    }
}