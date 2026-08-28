namespace CliUtilityServices.Pipes;

/// <summary>
/// Creates writable streams used for command output capture.
/// </summary>
internal interface IOutputStreamFactory
{
    /// <summary>
    /// Creates a writable asynchronous stream for the specified path.
    /// </summary>
    /// <param name="path">The destination file path.</param>
    /// <returns>The created writable stream.</returns>
    Stream Create(
        string path);
}