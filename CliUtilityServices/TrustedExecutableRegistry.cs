namespace CliUtilityServices.Security;

/// <summary>
/// Provides executable paths registered by trusted application configuration.
/// </summary>
public sealed class TrustedExecutableRegistry:ITrustedExecutableRegistry
{
    private readonly IReadOnlyDictionary<string, string> _executables;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrustedExecutableRegistry"/> class.
/// </summary>
    /// <param name="executables">The trusted executable mappings.</param>
    public TrustedExecutableRegistry(
        IReadOnlyDictionary<string, string> executables)
    {
        ArgumentNullException.ThrowIfNull(executables);

        _executables = executables;
    }

    /// <summary>
    /// Resolves a trusted executable identifier.
    /// </summary>
    /// <param name="name">The registered executable name.</param>
    /// <returns>The trusted executable path.</returns>
    public string Resolve(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (_executables.TryGetValue(name, out string? executable))
        {
            return executable;
        }

        throw new NotSupportedException(
            $"Executable '{name}' is not registered as trusted.");
    }
}