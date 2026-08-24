using System.IO.Abstractions;
using EnvironmentUtilityServices;

namespace CliUtilityServices.Security;

/// <summary>
/// Resolves and validates child-process working directories.
/// </summary>
public sealed class WorkingDirectoryResolver
    : IWorkingDirectoryResolver
{
    private readonly IFileSystem _fileSystem;
    private readonly IOsUtilityService _osUtilityService;
    private readonly string? _trustedRootDirectory;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="WorkingDirectoryResolver"/> class.
    /// </summary>
    /// <param name="fileSystem">
    /// The file system abstraction.
    /// </param>
    /// <param name="osUtilityService">
    /// The operating-system utility service used for
    /// platform-specific path comparison.
    /// </param>
    /// <param name="trustedRootDirectory">
    /// An optional trusted root directory.
    /// When supplied, resolved working directories must remain
    /// within this directory tree.
    /// </param>
    public WorkingDirectoryResolver(
        IFileSystem fileSystem,
        IOsUtilityService osUtilityService,
        string? trustedRootDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(
            fileSystem,
            nameof(fileSystem));

        ArgumentNullException.ThrowIfNull(
            osUtilityService,
            nameof(osUtilityService));

        _fileSystem = fileSystem;
        _osUtilityService = osUtilityService;

        _trustedRootDirectory =
            ResolveTrustedRootDirectory(
                trustedRootDirectory);
    }

    /// <inheritdoc />
    public string Resolve(
        string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            workingDirectory,
            nameof(workingDirectory));

        if (!_fileSystem.Path.IsPathFullyQualified(
                workingDirectory))
        {
            throw new InvalidOperationException(
                $"Working directory '{workingDirectory}' must be an absolute path.");
        }

        string fullPath;

        try
        {
            fullPath =
                _fileSystem.Path.GetFullPath(
                    workingDirectory);
        }
        catch (Exception ex)
            when (
                ex is ArgumentException
                or NotSupportedException
                or PathTooLongException)
        {
            throw new InvalidOperationException(
                $"Working directory '{workingDirectory}' could not be normalized.",
                ex);
        }

        if (!_fileSystem.Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException(
                $"Working directory '{fullPath}' does not exist.");
        }

        ValidateTrustedRoot(fullPath);

        return fullPath;
    }

    /// <summary>
    /// Resolves and validates the optional trusted root directory.
    /// </summary>
    /// <param name="trustedRootDirectory">
    /// The configured trusted root directory.
    /// </param>
    /// <returns>
    /// The canonical trusted root directory, or null when no
    /// trusted root restriction is configured.
    /// </returns>
    private string? ResolveTrustedRootDirectory(
        string? trustedRootDirectory)
    {
        if (string.IsNullOrWhiteSpace(
                trustedRootDirectory))
        {
            return null;
        }

        if (!_fileSystem.Path.IsPathFullyQualified(
                trustedRootDirectory))
        {
            throw new ArgumentException(
                "Trusted root directory must be an absolute path.",
                nameof(trustedRootDirectory));
        }

        string fullPath =
            _fileSystem.Path.GetFullPath(
                trustedRootDirectory);

        if (!_fileSystem.Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException(
                $"Trusted root directory '{fullPath}' does not exist.");
        }

        return NormalizeDirectoryPath(fullPath);
    }

    /// <summary>
    /// Validates that the resolved working directory remains
    /// within the configured trusted root.
    /// </summary>
    /// <param name="resolvedWorkingDirectory">
    /// The canonical working directory.
    /// </param>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when the working directory escapes the trusted root.
    /// </exception>
    private void ValidateTrustedRoot(
        string resolvedWorkingDirectory)
    {
        if (_trustedRootDirectory is null)
        {
            return;
        }

        string normalizedWorkingDirectory =
            NormalizeDirectoryPath(
                resolvedWorkingDirectory);

        StringComparison comparison =
            _osUtilityService.GetComparison();

        if (normalizedWorkingDirectory.Equals(
                _trustedRootDirectory,
                comparison))
        {
            return;
        }

        if (normalizedWorkingDirectory.StartsWith(
                _trustedRootDirectory,
                comparison))
        {
            return;
        }

        throw new UnauthorizedAccessException(
            $"Working directory '{resolvedWorkingDirectory}' is outside the trusted root '{_trustedRootDirectory}'.");
    }

    /// <summary>
    /// Normalizes a directory path and ensures it has a trailing
    /// directory separator for safe descendant comparison.
    /// </summary>
    /// <param name="path">
    /// The directory path to normalize.
    /// </param>
    /// <returns>
    /// The normalized directory path.
    /// </returns>
    private string NormalizeDirectoryPath(
        string path)
    {
        string fullPath =
            _fileSystem.Path.GetFullPath(path);

        char separator =
            _fileSystem.Path.DirectorySeparatorChar;

        if (!fullPath.EndsWith(separator))
        {
            fullPath += separator;
        }

        return fullPath;
    }
}