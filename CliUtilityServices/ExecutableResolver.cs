using System.IO.Abstractions;

namespace CliUtilityServices.Security;

/// <summary>
/// Resolves executables and optionally enforces absolute executable paths.
/// </summary>
public sealed class ExecutableResolver : IExecutableResolver
{
    private readonly IFileSystem _fileSystem;
    private readonly bool _requireAbsolutePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutableResolver"/> class.
    /// </summary>
    /// <param name="fileSystem">The file system abstraction.</param>
    /// <param name="requireAbsolutePath">
    /// Indicates whether executable paths must be absolute.
    /// </param>
    public ExecutableResolver(
        IFileSystem fileSystem,
        bool requireAbsolutePath = true)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);

        _fileSystem = fileSystem;
        _requireAbsolutePath = requireAbsolutePath;
    }

    /// <inheritdoc />
    public string Resolve(string executable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executable);

        if (_requireAbsolutePath &&
            !_fileSystem.Path.IsPathFullyQualified(executable))
        {
            throw new InvalidOperationException(
                $"Executable '{executable}' must be specified using an absolute path.");
        }

        string fullPath = _fileSystem.Path.GetFullPath(executable);

        if (!_fileSystem.File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                $"Executable '{fullPath}' does not exist.",
                fullPath);
        }

        return fullPath;
    }
}