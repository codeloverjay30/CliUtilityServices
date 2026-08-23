namespace CliUtilityServices.Security;

/// <summary>
/// Provides predefined child-process environment security policies.
/// </summary>
public static class ChildEnvironmentPolicies
{
    /// <summary>
    /// Gets the backward-compatible environment policy.
    /// </summary>
    /// <remarks>
    /// The complete parent-process environment is inherited.
    /// This preset favors compatibility over environment isolation.
    /// </remarks>
    public static ChildEnvironmentPolicy Compatible =>
        new()
        {
            Mode = ChildEnvironmentMode.InheritAll
        };

    /// <summary>
    /// Gets an isolated child-process environment policy.
    /// </summary>
    /// <remarks>
    /// Parent-process environment variables are not intentionally inherited.
    /// Required variables must be supplied explicitly.
    /// </remarks>
    public static ChildEnvironmentPolicy Isolated =>
        new()
        {
            Mode = ChildEnvironmentMode.Isolated
        };
}