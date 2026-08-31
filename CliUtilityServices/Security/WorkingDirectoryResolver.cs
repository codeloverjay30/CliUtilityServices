using System.IO.Abstractions;
using EnvironmentUtilityServices;
using SymbolicLinkUtilityServices.Security;

namespace CliUtilityServices.Security;

/// <summary>
/// Resolves and validates child-process working directories.
/// </summary>
public sealed class WorkingDirectoryResolver
    : IWorkingDirectoryResolver
{
    private readonly IFileSystem _fileSystem;
    private readonly IOsUtilityService _osUtilityService;
    private readonly IPathLinkValidator _pathLinkValidator;
    private readonly string? _trustedRootDirectory;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="WorkingDirectoryResolver"/> class using the default
    /// filesystem-indirection validator.
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
    /// within this directory tree and must not traverse filesystem
    /// indirection such as symbolic links or junctions.
    /// </param>
    public WorkingDirectoryResolver(
        IFileSystem fileSystem,
        IOsUtilityService osUtilityService,
        string? trustedRootDirectory = null)
        : this(
            fileSystem,
            osUtilityService,
            trustedRootDirectory,
            new PathLinkValidator(fileSystem))
    {
    }

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="WorkingDirectoryResolver"/> class using the specified
    /// filesystem-indirection validator.
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
    /// </param>
    /// <param name="pathLinkValidator">
    /// The validator used to reject symbolic-link, junction,
    /// and reparse-point traversal.
    /// </param>
    public WorkingDirectoryResolver(
        IFileSystem fileSystem,
        IOsUtilityService osUtilityService,
        string? trustedRootDirectory,
        IPathLinkValidator pathLinkValidator)
    {
        ArgumentNullException.ThrowIfNull(
            fileSystem,
            nameof(fileSystem));

        ArgumentNullException.ThrowIfNull(
            osUtilityService,
            nameof(osUtilityService));

        ArgumentNullException.ThrowIfNull(
            pathLinkValidator,
            nameof(pathLinkValidator));

        _fileSystem = fileSystem;
        _osUtilityService = osUtilityService;
        _pathLinkValidator = pathLinkValidator;

        _trustedRootDirectory =
            ResolveTrustedRootDirectory(
                trustedRootDirectory);

        /*
         * Validate the trusted root itself immediately so that a trusted
         * root backed by a symbolic link, junction, or reparse point is
         * rejected at configuration time.
         *
         * The same validation is repeated during Resolve() to defend
         * against filesystem changes that occur after construction.
         */
        if (_trustedRootDirectory is not null)
        {
            _pathLinkValidator.ValidateNoPathIndirection(
                _trustedRootDirectory,
                _trustedRootDirectory);
        }
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

        if (!_fileSystem.Directory.Exists(
                fullPath))
        {
            throw new DirectoryNotFoundException(
                $"Working directory '{fullPath}' does not exist.");
        }

        ValidateTrustedRoot(
            fullPath);

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

        string fullPath;

        try
        {
            fullPath =
                _fileSystem.Path.GetFullPath(
                    trustedRootDirectory);
        }
        catch (Exception ex)
            when (
                ex is ArgumentException
                or NotSupportedException
                or PathTooLongException)
        {
            throw new InvalidOperationException(
                $"Trusted root directory '{trustedRootDirectory}' could not be normalized.",
                ex);
        }

        if (!_fileSystem.Directory.Exists(
                fullPath))
        {
            throw new DirectoryNotFoundException(
                $"Trusted root directory '{fullPath}' does not exist.");
        }

        return NormalizeDirectoryPath(
            fullPath);
    }

    /// <summary>
    /// Validates that the resolved working directory remains within the
    /// configured trusted root and does not traverse filesystem indirection.
    /// </summary>
    /// <param name="resolvedWorkingDirectory">
    /// The canonical working directory.
    /// </param>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when the working directory escapes the trusted root or
    /// traverses symbolic links, junctions, or reparse points.
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

        bool isTrustedRoot =
            normalizedWorkingDirectory.Equals(
                _trustedRootDirectory,
                comparison);

        bool isTrustedDescendant =
            normalizedWorkingDirectory.StartsWith(
                _trustedRootDirectory,
                comparison);

        if (!isTrustedRoot
            && !isTrustedDescendant)
        {
            throw new UnauthorizedAccessException(
                $"Working directory '{resolvedWorkingDirectory}' is outside " +
                $"the trusted root '{_trustedRootDirectory}'.");
        }

        /*
         * Lexical containment alone is insufficient because a path inside
         * the trusted root may contain a symbolic link or junction that
         * redirects filesystem access outside the trusted root.
         */
        _pathLinkValidator.ValidateNoPathIndirection(
            _trustedRootDirectory,
            normalizedWorkingDirectory);
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
            _fileSystem.Path.GetFullPath(
                path);

        char separator =
            _fileSystem.Path.DirectorySeparatorChar;

        if (!fullPath.EndsWith(
                separator))
        {
            fullPath += separator;
        }

        return fullPath;
    }
}