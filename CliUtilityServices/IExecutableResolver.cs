namespace CliUtilityServices.Security;

/// <summary>
/// Resolves executable identifiers into validated executable paths.
/// </summary>
public interface IExecutableResolver
{
    /// <summary>
    /// Resolves and validates the specified executable.
    /// </summary>
    /// <param name="executable">The executable name or path.</param>
    /// <returns>The validated executable path.</returns>
    string Resolve(string executable);
}