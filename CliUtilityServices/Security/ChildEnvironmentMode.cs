namespace CliUtilityServices.Security;

/// <summary>
/// Defines how environment variables are inherited by a child process.
/// </summary>
public enum ChildEnvironmentMode
{
    /// <summary>
    /// Inherits the complete parent process environment.
    /// </summary>
    InheritAll = 16,

    /// <summary>
    /// Inherits only explicitly allowed parent environment variables.
    /// </summary>
    AllowInheritedList = 8,

    /// <summary>
    /// Explicitly allow specific environment variables.
    /// </summary>
    AllowList = 2,

    /// <summary>
    /// Explicitly denies specific environment variables.
    /// </summary>
    /// <remarks>
    /// A deny-list should not be treated as the sole security boundary because
    /// unknown sensitive environment variables cannot be enumerated reliably.
    /// Prefer <see cref="AllowList"/>, <see cref="AllowInheritedList"/>, or
    /// <see cref="Isolated"/> when stronger environment isolation is required.
    /// This enumeration does not support bitwise-combined modes.
    /// </remarks>
    DenyList = 1,

    /// <summary>
    /// Does not intentionally inherit parent environment variables.
    /// </summary>
    Isolated = 4
}