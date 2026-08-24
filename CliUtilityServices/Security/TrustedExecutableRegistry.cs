using System.Collections.Frozen;

namespace CliUtilityServices.Security;

/// <summary>
/// Provides trusted executable mappings and validates resolved executable paths
/// before exposing them to the process execution pipeline.
/// </summary>
public sealed class TrustedExecutableRegistry : ITrustedExecutableRegistry
{
    private readonly FrozenDictionary<string, string> _executables;
    private readonly IExecutableResolver _pathResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrustedExecutableRegistry"/> class.
    /// </summary>
    /// <param name="executables">
    /// The trusted executable identifier-to-path mappings supplied by trusted
    /// application configuration.
    /// </param>
    /// <param name="pathResolver">
    /// The executable resolver used to validate each registered executable path.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="executables"/> or
    /// <paramref name="pathResolver"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when a registered executable identifier or path is null,
    /// empty, or consists only of white-space characters.
    /// </exception>
    public TrustedExecutableRegistry(
        IReadOnlyDictionary<string, string> executables,
        IExecutableResolver pathResolver)
    {
        ArgumentNullException.ThrowIfNull(
            executables,
            nameof(executables));

        ArgumentNullException.ThrowIfNull(
            pathResolver,
            nameof(pathResolver));

        ValidateMappings(executables);

        _executables = executables.ToFrozenDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);

        _pathResolver = pathResolver;
    }

    /// <summary>
    /// Resolves a trusted executable identifier and validates the registered
    /// executable path before returning it.
    /// </summary>
    /// <param name="name">
    /// The trusted executable identifier registered by application configuration.
    /// </param>
    /// <returns>
    /// The validated executable path associated with the trusted identifier.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> is null, empty, or consists only
    /// of white-space characters.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when the executable identifier is not registered as trusted.
    /// </exception>
    public string Resolve(string name)
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
    /// Validates trusted executable mappings before storing them.
    /// </summary>
    /// <param name="executables">
    /// The executable mappings to validate.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when a registered executable identifier or path is invalid.
    /// </exception>
    private static void ValidateMappings(
        IReadOnlyDictionary<string, string> executables)
    {
        foreach (KeyValuePair<string, string> pair in executables)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
            {
                throw new ArgumentException(
                    "Trusted executable identifiers cannot be null, empty, or whitespace.",
                    nameof(executables));
            }

            if (string.IsNullOrWhiteSpace(pair.Value))
            {
                throw new ArgumentException(
                    $"The executable path registered for '{pair.Key}' cannot be null, empty, or whitespace.",
                    nameof(executables));
            }
        }
    }
}