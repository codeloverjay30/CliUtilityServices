namespace CliUtilityServices.Tests.Pipes;

/// <summary>
/// Provides a read-only test stream for constructor validation.
/// </summary>
internal sealed class NonWritableStream : Stream
{
    /// <inheritdoc />
    public override bool CanRead => true;

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public override bool CanWrite => false;

    /// <inheritdoc />
    public override long Length => 0;

    /// <inheritdoc />
    public override long Position
    {
        get => 0;

        set => throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Flush()
    {
    }

    /// <inheritdoc />
    public override int Read(
        byte[] buffer,
        int offset,
        int count)
    {
        return 0;
    }

    /// <inheritdoc />
    public override long Seek(
        long offset,
        SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void SetLength(
        long value)
    {
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public override void Write(
        byte[] buffer,
        int offset,
        int count)
    {
        throw new NotSupportedException();
    }
}
