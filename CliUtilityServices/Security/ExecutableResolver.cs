using System.IO.Abstractions;

namespace CliUtilityServices.Security;

/// <summary>
/// Resolves executable identifiers into validated canonical executable paths.
/// </summary>
/// <remarks>
/// In <see cref="ExecutableResolutionMode.PathLookup"/> mode, fully qualified
/// paths are validated directly while executable names are searched using the
/// current process PATH. On Windows, PATHEXT is used when the requested
/// executable does not already have an extension.
/// </remarks>
public sealed class ExecutableResolver : IExecutableResolver
{
    private const string PathVariableName = "PATH";
    private const string PathExtensionsVariableName = "PATHEXT";

    private static readonly string[] DefaultWindowsExecutableExtensions =
    [
        ".COM",
        ".EXE",
        ".BAT",
        ".CMD"
    ];

    private readonly IFileSystem _fileSystem;
    private readonly IProcessEnvironmentSource _environmentSource;
    private readonly ExecutableResolutionMode _mode;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="ExecutableResolver"/> class.
    /// </summary>
    /// <param name="fileSystem">
    /// The file system abstraction used to inspect executable paths.
    /// </param>
    /// <param name="environmentSource">
    /// The source used to obtain the parent process environment.
    /// </param>
    /// <param name="mode">
    /// The executable resolution policy.
    /// </param>
    public ExecutableResolver(
        IFileSystem fileSystem,
        IProcessEnvironmentSource environmentSource,
        ExecutableResolutionMode mode =
            ExecutableResolutionMode.PathLookup)
    {
        ArgumentNullException.ThrowIfNull(
            fileSystem,
            nameof(fileSystem));

        ArgumentNullException.ThrowIfNull(
            environmentSource,
            nameof(environmentSource));

        _fileSystem = fileSystem;
        _environmentSource = environmentSource;
        _mode = mode;
    }

    /// <inheritdoc />
    public string Resolve(
        string executable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            executable,
            nameof(executable));

        if (_fileSystem.Path.IsPathFullyQualified(
                executable))
        {
            return ValidateAbsolutePath(
                executable);
        }

        if (_mode ==
            ExecutableResolutionMode.RequireAbsolutePath)
        {
            throw new InvalidOperationException(
                $"Executable '{executable}' must be specified using an absolute path.");
        }

        return ResolveFromPath(
            executable);
    }

    /// <summary>
    /// Resolves an executable name using the current process PATH.
    /// </summary>
    /// <param name="executable">
    /// The executable name to locate.
    /// </param>
    /// <returns>
    /// The canonical absolute executable path.
    /// </returns>
    /// <exception cref="FileNotFoundException">
    /// Thrown when the executable cannot be located.
    /// </exception>
    private string ResolveFromPath(
        string executable)
    {
        IReadOnlyDictionary<string, string?> environment =
            _environmentSource.GetEnvironmentVariables();

        string? path =
            GetEnvironmentVariable(
                environment,
                PathVariableName);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new FileNotFoundException(
                $"Executable '{executable}' could not be resolved because PATH is empty or unavailable.",
                executable);
        }

        string[] executableNames =
            GetExecutableCandidates(
                executable,
                environment);

        foreach (ReadOnlySpan<char> pathEntry in
                 EnumeratePathEntries(path))
        {
            if (pathEntry.IsEmpty)
            {
                continue;
            }

            string directory =
                pathEntry.ToString();

            if (!_fileSystem.Path.IsPathFullyQualified(
                    directory))
            {
                continue;
            }

            string fullDirectory;

            try
            {
                fullDirectory =
                    _fileSystem.Path.GetFullPath(
                        directory);
            }
            catch (Exception ex)
                when (
                    ex is ArgumentException
                    or NotSupportedException
                    or PathTooLongException)
            {
                continue;
            }

            foreach (string executableName in executableNames)
            {
                string candidate =
                    _fileSystem.Path.Combine(
                        fullDirectory,
                        executableName);

                if (!_fileSystem.File.Exists(candidate))
                {
                    continue;
                }

                return ValidateAbsolutePath(
                    candidate);
            }
        }

        throw new FileNotFoundException(
            $"Executable '{executable}' could not be found in PATH.",
            executable);
    }

    /// <summary>
    /// Produces executable file-name candidates for the current platform.
    /// </summary>
    /// <param name="executable">
    /// The requested executable name.
    /// </param>
    /// <param name="environment">
    /// The current process environment snapshot.
    /// </param>
    /// <returns>
    /// Executable names to search for in each PATH directory.
    /// </returns>
    private string[] GetExecutableCandidates(
        string executable,
        IReadOnlyDictionary<string, string?> environment)
    {
        if (!OperatingSystem.IsWindows())
        {
            return [executable];
        }

        if (_fileSystem.Path.HasExtension(
                executable))
        {
            return [executable];
        }

        string? pathExtensions =
            GetEnvironmentVariable(
                environment,
                PathExtensionsVariableName);

        string[] extensions =
            ParseWindowsExecutableExtensions(
                pathExtensions);

        var candidates =
            new string[extensions.Length];

        for (int index = 0;
             index < extensions.Length;
             index++)
        {
            candidates[index] =
                executable + extensions[index];
        }

        return candidates;
    }

    /// <summary>
    /// Parses the Windows PATHEXT environment variable into normalized
    /// executable extensions.
    /// </summary>
    /// <param name="pathExtensions">
    /// The PATHEXT environment variable value.
    /// </param>
    /// <returns>
    /// The normalized executable extensions.
    /// </returns>
    private static string[] ParseWindowsExecutableExtensions(
        string? pathExtensions)
    {
        if (string.IsNullOrWhiteSpace(
                pathExtensions))
        {
            return DefaultWindowsExecutableExtensions;
        }

        var extensions =
            new List<string>();

        ReadOnlySpan<char> remaining =
            pathExtensions.AsSpan();

        while (!remaining.IsEmpty)
        {
            int separatorIndex =
                remaining.IndexOf(';');

            ReadOnlySpan<char> extension =
                separatorIndex < 0
                    ? remaining
                    : remaining[..separatorIndex];

            extension = extension.Trim();

            if (!extension.IsEmpty)
            {
                string normalized =
                    extension[0] == '.'
                        ? extension.ToString()
                        : string.Concat(
                            ".",
                            extension);

                if (!extensions.Contains(
                        normalized,
                        StringComparer.OrdinalIgnoreCase))
                {
                    extensions.Add(normalized);
                }
            }

            if (separatorIndex < 0)
            {
                break;
            }

            remaining =
                remaining[(separatorIndex + 1)..];
        }

        return extensions.Count == 0
            ? DefaultWindowsExecutableExtensions
            : extensions.ToArray();
    }

    /// <summary>
    /// Enumerates PATH entries without allocating an intermediate split array.
    /// </summary>
    /// <param name="path">
    /// The PATH environment variable value.
    /// </param>
    /// <returns>
    /// The PATH entries.
    /// </returns>
    private static PathEntryEnumerable EnumeratePathEntries(
        string path)
    {
        return new PathEntryEnumerable(
            path);
    }

    /// <summary>
    /// Gets an environment variable using platform-appropriate name
    /// comparison semantics.
    /// </summary>
    /// <param name="environment">
    /// The environment snapshot.
    /// </param>
    /// <param name="name">
    /// The environment variable name.
    /// </param>
    /// <returns>
    /// The environment variable value, or null when unavailable.
    /// </returns>
    private static string? GetEnvironmentVariable(
        IReadOnlyDictionary<string, string?> environment,
        string name)
    {
        if (environment.TryGetValue(
                name,
                out string? value))
        {
            return value;
        }

        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        foreach (KeyValuePair<string, string?> pair
                 in environment)
        {
            if (string.Equals(
                    pair.Key,
                    name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Validates and canonicalizes a fully qualified executable path.
    /// </summary>
    /// <param name="executable">
    /// The executable path to validate.
    /// </param>
    /// <returns>
    /// The canonical absolute executable path.
    /// </returns>
    private string ValidateAbsolutePath(
        string executable)
    {
        string fullPath;

        try
        {
            fullPath =
                _fileSystem.Path.GetFullPath(
                    executable);
        }
        catch (Exception ex)
            when (
                ex is ArgumentException
                or NotSupportedException
                or PathTooLongException)
        {
            throw new InvalidOperationException(
                $"Executable '{executable}' could not be normalized.",
                ex);
        }

        if (!_fileSystem.File.Exists(
                fullPath))
        {
            throw new FileNotFoundException(
                $"Executable '{fullPath}' does not exist.",
                fullPath);
        }

        return fullPath;
    }

    /// <summary>
    /// Provides allocation-free enumeration over PATH entries.
    /// </summary>
    private readonly ref struct PathEntryEnumerable
    {
        private readonly ReadOnlySpan<char> _path;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="PathEntryEnumerable"/> structure.
        /// </summary>
        /// <param name="path">
        /// The PATH value to enumerate.
        /// </param>
        public PathEntryEnumerable(
            string path)
        {
            _path = path.AsSpan();
        }

        /// <summary>
        /// Creates an enumerator over the PATH entries.
        /// </summary>
        /// <returns>
        /// The PATH entry enumerator.
        /// </returns>
        public PathEntryEnumerator GetEnumerator()
        {
            return new PathEntryEnumerator(
                _path);
        }
    }

    /// <summary>
    /// Enumerates PATH entries without allocating an intermediate array.
    /// </summary>
    private ref struct PathEntryEnumerator
    {
        private ReadOnlySpan<char> _remaining;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="PathEntryEnumerator"/> structure.
        /// </summary>
        /// <param name="path">
        /// The PATH value to enumerate.
        /// </param>
        public PathEntryEnumerator(
            ReadOnlySpan<char> path)
        {
            _remaining = path;
            Current = default;
        }

        /// <summary>
        /// Gets the current PATH entry.
        /// </summary>
        public ReadOnlySpan<char> Current { get; private set; }

        /// <summary>
        /// Advances to the next PATH entry.
        /// </summary>
        /// <returns>
        /// True when another entry is available; otherwise false.
        /// </returns>
        public bool MoveNext()
        {
            if (_remaining.IsEmpty)
            {
                return false;
            }

            int separatorIndex =
                _remaining.IndexOf(
                    Path.PathSeparator);

            if (separatorIndex < 0)
            {
                Current =
                    _remaining.Trim();

                _remaining =
                    ReadOnlySpan<char>.Empty;

                return true;
            }

            Current =
                _remaining[..separatorIndex]
                    .Trim();

            _remaining =
                _remaining[
                    (separatorIndex + 1)..];

            return true;
        }
    }
}