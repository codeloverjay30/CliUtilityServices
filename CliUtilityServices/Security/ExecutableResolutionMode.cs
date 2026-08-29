namespace CliUtilityServices.Security;

/// <summary>
/// Defines how executable identifiers are resolved before process creation.
/// </summary>
public enum ExecutableResolutionMode
{
    /// <summary>
    /// Resolves executable names using the current process PATH and
    /// platform-specific executable naming conventions.
    /// </summary>
    PathLookup = 0,

    /// <summary>
    /// Requires callers to provide a fully qualified executable path.
    /// </summary>
    RequireAbsolutePath = 1
}