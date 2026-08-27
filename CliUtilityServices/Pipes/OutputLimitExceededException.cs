namespace CliUtilityServices.Pipes;

/// <summary>
/// Represents an error that occurs when command output exceeds
/// the configured write-time byte limit.
/// </summary>
public sealed class OutputLimitExceededException : IOException
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="OutputLimitExceededException"/> class.
    /// </summary>
    /// <param name="streamName">
    /// The logical output stream name.
    /// </param>
    /// <param name="maximumBytes">
    /// The maximum permitted number of bytes.
    /// </param>
    /// <param name="attemptedBytes">
    /// The number of bytes that would have been written.
    /// </param>
    public OutputLimitExceededException(
        string streamName,
        long maximumBytes,
        long attemptedBytes)
        : base(
            $"Command {streamName} exceeded the configured output limit " +
            $"of {maximumBytes} bytes. The attempted total was {attemptedBytes} bytes.")
    {
        StreamName = streamName;
        MaximumBytes = maximumBytes;
        AttemptedBytes = attemptedBytes;
    }

    /// <summary>
    /// Gets the logical output stream name.
    /// </summary>
    public string StreamName { get; }

    /// <summary>
    /// Gets the configured maximum number of bytes.
    /// </summary>
    public long MaximumBytes { get; }

    /// <summary>
    /// Gets the number of bytes that would have been written.
    /// </summary>
    public long AttemptedBytes { get; }
}