namespace CliUtilityServices.Security;

/// <summary>
/// Provides access to the current parent-process environment.
/// </summary>
public interface IProcessEnvironmentSource
{
    /// <summary>
    /// Gets a snapshot of the current process environment variables.
    /// </summary>
    /// <returns>A snapshot of the current environment.</returns>
    IReadOnlyDictionary<string, string?> GetEnvironmentVariables();
}