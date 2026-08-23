/// <summary>
/// Defines how environment variables are inherited by a child process.
/// </summary>
public enum ChildEnvironmentMode
{
    /// <summary>
    /// Inherits the complete parent process environment.
    /// </summary>
    InheritAll,

    /// <summary>
    /// Inherits only explicitly allowed parent environment variables.
    /// </summary>
    AllowInheritedList,

    /// <summary>
    /// Explicitly allow specific environment variables.
    /// </summary>
    AllowList,

    /// <summary>
    /// Explicitly deny specific environment variables.
    /// </summary>
    /// <remarks>
    /// NEVER consider it as the ONLY DEFENSIVE MODE
    /// since the blacklist is numerous, and we can't enumerate it infinitely.
    /// Thus, ALWAYS use it with other modes (using flags) 
    /// </remark>
    DenyList,

    /// <summary>
    /// Does not intentionally inherit parent environment variables.
    /// </summary>
    Isolated
}