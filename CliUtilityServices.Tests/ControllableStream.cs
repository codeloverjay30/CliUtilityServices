namespace CliUtilityServices.Tests;

internal sealed class ControllableStream : Stream
{
    private readonly MemoryStream _innerStream =
        new();

    private readonly Exception? _flushException;

    public ControllableStream(
        Exception? flushException = null)
    {
        _flushException =
            flushException;
    }

    public bool WasDisposed { get; private set; }

    public bool FlushWasCalled { get; private set; }

    public override bool CanRead =>
        _innerStream.CanRead;

    public override bool CanSeek =>
        _innerStream.CanSeek;

    public override bool CanWrite =>
        _innerStream.CanWrite;

    public override long Length =>
        _innerStream.Length;

    public override long Position
    {
        get => _innerStream.Position;
        set => _innerStream.Position = value;
    }

    public override void Flush()
    {
        FlushWasCalled = true;

        if (_flushException is not null)
        {
            throw _flushException;
        }

        _innerStream.Flush();
    }

    public override Task FlushAsync(
        CancellationToken cancellationToken)
    {
        FlushWasCalled = true;

        if (_flushException is not null)
        {
            return Task.FromException(
                _flushException);
        }

        return _innerStream.FlushAsync(
            cancellationToken);
    }

    public override int Read(
        byte[] buffer,
        int offset,
        int count)
    {
        return _innerStream.Read(
            buffer,
            offset,
            count);
    }

    public override long Seek(
        long offset,
        SeekOrigin origin)
    {
        return _innerStream.Seek(
            offset,
            origin);
    }

    public override void SetLength(
        long value)
    {
        _innerStream.SetLength(
            value);
    }

    public override void Write(
        byte[] buffer,
        int offset,
        int count)
    {
        _innerStream.Write(
            buffer,
            offset,
            count);
    }

    protected override void Dispose(
        bool disposing)
    {
        if (disposing)
        {
            WasDisposed = true;
            _innerStream.Dispose();
        }

        base.Dispose(
            disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        WasDisposed = true;

        await _innerStream
            .DisposeAsync()
            .ConfigureAwait(false);

        GC.SuppressFinalize(this);
    }
}
