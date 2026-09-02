
namespace CliUtilityServices.Security;

/// <summary>
/// Defines how environment variables are inherited by a child process.
/// </summary>
public enum ChildEnvironmentMode
{
    /// <summary>
    /// Inherits the complete parent process environment.
    /// </summary>
    InheritAll = 16 ,

    /// <summary>
    /// Inherits only explicitly allowed parent environment variables.
    /// </summary>
    AllowInheritedList = 8,

    /// <summary>
    /// Explicitly allow specific environment variables.
    /// </summary>
    AllowList = 2,

    /// <summary>
    /// Explicitly deny specific environment variables.
    /// </summary>
    /// <remarks>
    /// NEVER consider it as the ONLY DEFENSIVE MODE
    /// since the blacklist is numerous, and we can't enumerate it infinitely.
    /// Thus, ALWAYS use it with other modes (using flags) 
    /// </remarks>
    DenyList = 1,

    /// <summary>
    /// Does not intentionally inherit parent environment variables.
    /// </summary>
    Isolated = 4
}