namespace CliUtilityServices.Security;

/// <summary>
/// Resolves the effective environment variables for a child process.
/// </summary>
public interface IChildEnvironmentResolver
{
    /// <summary>
    /// Resolves the environment variables that should be supplied
    /// to the child process.
    /// </summary>
    /// <param name="policy">The child environment policy.</param>
    /// <returns>The resolved child process environment.</returns>
    IReadOnlyDictionary<string, string?> Resolve(
        ChildEnvironmentPolicy policy);
}
