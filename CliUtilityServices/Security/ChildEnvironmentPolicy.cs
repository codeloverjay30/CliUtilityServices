/// <summary>
/// Defines the environment inheritance policy for a child process.
/// </summary>
public sealed record ChildEnvironmentPolicy
{
    /// <summary>
    /// Gets the environment inheritance mode.
    /// </summary>
    public ChildEnvironmentMode Mode { get; init; }
        = ChildEnvironmentMode.InheritAll;

    /// <summary>
    /// Gets the parent environment variable names that may be inherited
    /// when allow-list mode is enabled.
    /// </summary>
    public IReadOnlySet<string> AllowedInheritedVariables { get; init; }
        = new HashSet<string>();

    /// <summary>
    /// Gets the allowed environment variable names
    /// when allow-list mode is enabled.
    /// </summary>
    public IReadOnlySet<string> AllowedVariables { get; init; }
        = new HashSet<string>();

    /// <summary>
    /// Gets the denied environment variable names
    /// when deny-list mode is enabled.
    /// </summary>
    public IReadOnlySet<string> DeniedVariables { get; init; }
        = new HashSet<string>();

    /// <summary>
    /// Gets explicit environment variable overrides for the child process.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Overrides { get; init; }
        = new Dictionary<string, string?>();
}