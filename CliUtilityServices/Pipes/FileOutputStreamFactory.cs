using System.IO.Abstractions;

namespace CliUtilityServices.Pipes;

/// <summary>
/// Creates file-backed streams for command output capture.
/// </summary>
internal sealed class FileOutputStreamFactory
    : IOutputStreamFactory
{
    private readonly IFileSystem _fileSystem;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="FileOutputStreamFactory"/> class.
    /// </summary>
    /// <param name="fileSystem">
    /// The file system abstraction.
    /// </param>
    public FileOutputStreamFactory(
        IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(
            fileSystem);

        _fileSystem =
            fileSystem;
    }

    /// <inheritdoc />
    public Stream Create(
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            path);

        return _fileSystem.File.Open(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
    }
}