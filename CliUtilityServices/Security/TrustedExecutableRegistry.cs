using System.Collections.Frozen;
using System.IO.Abstractions;

namespace CliUtilityServices.Security;

/// <summary>
/// Provides trusted executable mappings whose configured executable identities
/// are pinned to fully qualified paths.
/// </summary>
public sealed class TrustedExecutableRegistry
    : ITrustedExecutableRegistry
{
    private readonly FrozenDictionary<string, string> _executables;
    private readonly IExecutableResolver _pathResolver;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="TrustedExecutableRegistry"/> class.
    /// </summary>
    /// <param name="executables">
    /// The trusted executable identifier-to-path mappings supplied by trusted
    /// application configuration. Every registered executable path must be
    /// fully qualified.
    /// </param>
    /// <param name="pathResolver">
    /// The executable resolver used to validate and canonicalize registered
    /// executable paths when they are resolved.
    /// </param>
    /// <param name="fileSystem">
    /// The file-system abstraction used to validate trusted executable paths.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="executables"/>,
    /// <paramref name="pathResolver"/>, or <paramref name="fileSystem"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a registered executable identifier or path is invalid,
    /// or when a registered executable path is not fully qualified.
    /// </exception>
    public TrustedExecutableRegistry(
        IReadOnlyDictionary<string, string> executables,
        IExecutableResolver pathResolver,
        IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(
            executables,
            nameof(executables));

        ArgumentNullException.ThrowIfNull(
            pathResolver,
            nameof(pathResolver));

        ArgumentNullException.ThrowIfNull(
            fileSystem,
            nameof(fileSystem));

        ValidateMappings(
            executables,
            fileSystem);

        _executables =
            executables.ToFrozenDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal);

        _pathResolver = pathResolver;
    }

    /// <summary>
    /// Resolves a trusted executable identifier and validates the pinned
    /// executable path before returning it.
    /// </summary>
    /// <param name="name">
    /// The trusted executable identifier registered by application configuration.
    /// </param>
    /// <returns>
    /// The validated canonical executable path associated with the trusted
    /// identifier.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is null, empty, or whitespace.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when the executable identifier is not registered as trusted.
    /// </exception>
    public string Resolve(
        string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            name,
            nameof(name));

        if (!_executables.TryGetValue(
                name,
                out string? configuredExecutablePath))
        {
            throw new NotSupportedException(
                $"Executable '{name}' is not registered as trusted.");
        }

        return _pathResolver.Resolve(
            configuredExecutablePath);
    }

    /// <summary>
    /// Validates trusted executable mappings before they cross the trusted
    /// configuration boundary.
    /// </summary>
    /// <param name="executables">
    /// The executable mappings to validate.
    /// </param>
    /// <param name="fileSystem">
    /// The file-system abstraction used to inspect path qualification.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when a registered executable identifier or path is invalid,
    /// or when a registered executable path is not fully qualified.
    /// </exception>
    private static void ValidateMappings(
        IReadOnlyDictionary<string, string> executables,
        IFileSystem fileSystem)
    {
        foreach (KeyValuePair<string, string> pair in executables)
        {
            ValidateIdentifier(
                pair.Key,
                executables);

            ValidateExecutablePath(
                pair.Key,
                pair.Value,
                executables,
                fileSystem);
        }
    }

    /// <summary>
    /// Validates a trusted executable identifier.
    /// </summary>
    /// <param name="identifier">
    /// The executable identifier to validate.
    /// </param>
    /// <param name="executables">
    /// The mappings associated with the validation operation.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when the identifier is null, empty, or whitespace.
    /// </exception>
    private static void ValidateIdentifier(
        string? identifier,
        IReadOnlyDictionary<string, string> executables)
    {
        if (string.IsNullOrWhiteSpace(
                identifier))
        {
            throw new ArgumentException(
                "Trusted executable identifiers cannot be null, empty, or whitespace.",
                nameof(executables));
        }
    }

    /// <summary>
    /// Validates that a trusted executable path is non-empty and fully
    /// qualified so that executable identity cannot depend on PATH lookup
    /// or the current working directory.
    /// </summary>
    /// <param name="identifier">
    /// The trusted executable identifier associated with the path.
    /// </param>
    /// <param name="executablePath">
    /// The executable path to validate.
    /// </param>
    /// <param name="executables">
    /// The mappings associated with the validation operation.
    /// </param>
    /// <param name="fileSystem">
    /// The file-system abstraction used to inspect path qualification.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when the path is null, empty, whitespace, or not fully qualified.
    /// </exception>
    private static void ValidateExecutablePath(
        string identifier,
        string? executablePath,
        IReadOnlyDictionary<string, string> executables,
        IFileSystem fileSystem)
    {
        if (string.IsNullOrWhiteSpace(
                executablePath))
        {
            throw new ArgumentException(
                $"The executable path registered for '{identifier}' cannot be null, empty, or whitespace.",
                nameof(executables));
        }

        bool isFullyQualified;

        try
        {
            isFullyQualified =
                fileSystem.Path.IsPathFullyQualified(
                    executablePath);
        }
        catch (Exception ex)
            when (
                ex is ArgumentException
                or NotSupportedException
                or PathTooLongException)
        {
            throw new ArgumentException(
                $"The executable path registered for '{identifier}' is invalid.",
                nameof(executables),
                ex);
        }

        if (!isFullyQualified)
        {
            throw new ArgumentException(
                $"The executable path registered for '{identifier}' must be fully qualified.",
                nameof(executables));
        }
    }
}