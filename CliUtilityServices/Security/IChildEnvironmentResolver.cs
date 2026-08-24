namespace CliUtilityServices.Security;

/// <summary>
/// Resolves the effective environment mutations applied to a child process.
/// </summary>
public interface IChildEnvironmentResolver
{
    /// <summary>
    /// Resolves environment mutations required to enforce the specified
    /// child-process environment policy.
    /// </summary>
    /// <param name="policy">The child environment policy.</param>
    /// <param name="explicitVariables">
    /// Explicit environment variables requested for the child process.
    /// </param>
    /// <returns>
    /// Environment variable mutations where null values indicate removal
    /// from the inherited environment.
    /// </returns>
    IReadOnlyDictionary<string, string?> Resolve(
        ChildEnvironmentPolicy policy,
        IReadOnlyDictionary<string, string?> explicitVariables);
}