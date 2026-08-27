namespace CliUtilityServices;

/// <summary>
/// Defines output size limits for a command execution.
/// </summary>
public sealed record CommandOutputLimits
{
    /// <summary>
    /// Gets the maximum number of bytes permitted on standard output.
    /// </summary>
    public long MaxStandardOutputBytes { get; init; }
        = 50L * 1024 * 1024;

    /// <summary>
    /// Gets the maximum number of bytes permitted on standard error.
    /// </summary>
    public long MaxStandardErrorBytes { get; init; }
        = 50L * 1024 * 1024;
}
