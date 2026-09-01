using CliUtilityServices.Pipes;

namespace CliUtilityServices.Tests.Pipes;

/// <summary>
/// Provides deterministic output streams to the system under test without
/// requiring a dynamic proxy for the internal
/// <see cref="IOutputStreamFactory"/> contract.
/// </summary>
public sealed partial class SequentialOutputStreamFactory :
    IOutputStreamFactory
{
    private readonly Queue<Func<Stream>>
        _streamFactories;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="SequentialOutputStreamFactory"/> class.
    /// </summary>
    /// <param name="streamFactories">
    /// The ordered stream factories to invoke.
    /// </param>
    public SequentialOutputStreamFactory(
        params Func<Stream>[] streamFactories)
    {
        ArgumentNullException.ThrowIfNull(
            streamFactories);

        _streamFactories =
            new Queue<Func<Stream>>(
                streamFactories);
    }

    /// <summary>
    /// Gets the number of calls made to <see cref="Create"/>.
    /// </summary>
    public int CreateCount
    {
        get;
        private set;
    }

    /// <summary>
    /// Creates the next configured output stream.
    /// </summary>
    /// <param name="path">
    /// The destination path requested by the system under test.
    /// </param>
    /// <returns>
    /// The next configured stream.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no configured stream factory remains.
    /// </exception>
    public Stream Create(
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            path);

        CreateCount =
            checked(
                CreateCount + 1);

        if (_streamFactories.Count == 0)
        {
            throw new InvalidOperationException(
                "No output stream factory remains for this test.");
        }

        return _streamFactories
            .Dequeue()
            .Invoke();
    }
}
