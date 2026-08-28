using CliUtilityServices.Pipes;

public sealed class SequenceOutputStreamFactory
    : IOutputStreamFactory
{
    private readonly Queue<Stream> _streams;

    public SequenceOutputStreamFactory(
        params Stream[] streams)
    {
        ArgumentNullException.ThrowIfNull(
            streams);

        _streams =
            new Queue<Stream>(
                streams);
    }

    public Stream Create(
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            path);

        if (_streams.Count == 0)
        {
            throw new InvalidOperationException(
                "No output stream remains in the test factory.");
        }

        return _streams.Dequeue();
    }
}