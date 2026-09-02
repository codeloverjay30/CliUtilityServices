using System.Collections.Frozen;

namespace CliUtilityServices.Security;

/// <summary>
/// Creates defensive child-process environment policy snapshots.
/// </summary>
public class ChildEnvironmentPolicyBuilder
{
    /// <summary>
    /// Creates an allow-list child environment policy.
    /// </summary>
    /// <param name="environmentVariables">
    /// The environment variable names permitted by the policy.
    /// </param>
    /// <returns>A defensive child environment policy snapshot.</returns>
    public static ChildEnvironmentPolicy CreateWithAllowListMode(
        IReadOnlySet<string> environmentVariables)
    {
        return new ChildEnvironmentPolicy
        {
            Mode = ChildEnvironmentMode.AllowList,
            AllowedVariables =
                CreateFrozenSet(
                    environmentVariables,
                    nameof(environmentVariables),
                    "Environment variables cannot be empty.")
        };
    }

    /// <summary>
    /// Creates a child environment policy that inherits only explicitly
    /// allowed parent environment variables.
    /// </summary>
    /// <param name="allowedInheritedVariables">
    /// The parent environment variable names permitted to be inherited.
    /// </param>
    /// <returns>A defensive child environment policy snapshot.</returns>
    public static ChildEnvironmentPolicy CreateWithAllowInheritedListMode(
        IReadOnlySet<string> allowedInheritedVariables)
    {
        return new ChildEnvironmentPolicy
        {
            Mode = ChildEnvironmentMode.AllowInheritedList,
            AllowedInheritedVariables =
                CreateFrozenSet(
                    allowedInheritedVariables,
                    nameof(allowedInheritedVariables),
                    "Allowed inherited variables cannot be empty.")
        };
    }

    /// <summary>
    /// Creates a child environment policy that inherits explicitly allowed
    /// parent variables while excluding explicitly denied variables.
    /// </summary>
    /// <param name="allowedInheritedVariables">
    /// The parent environment variable names permitted to be inherited.
    /// </param>
    /// <param name="deniedVariables">
    /// The environment variable names explicitly denied by the policy.
    /// </param>
    /// <returns>A defensive child environment policy snapshot.</returns>
    public static ChildEnvironmentPolicy CreateWithAllowInheritedListMode(
        IReadOnlySet<string> allowedInheritedVariables,
        IReadOnlySet<string> deniedVariables)
    {
        return new ChildEnvironmentPolicy
        {
            Mode = ChildEnvironmentMode.AllowInheritedList,
            AllowedInheritedVariables =
                CreateFrozenSet(
                    allowedInheritedVariables,
                    nameof(allowedInheritedVariables),
                    "Allowed inherited variables cannot be empty."),
            DeniedVariables =
                CreateFrozenSet(
                    deniedVariables,
                    nameof(deniedVariables),
                    "Denied variables cannot be empty.")
        };
    }

    /// <summary>
    /// Creates a deny-list child environment policy.
    /// </summary>
    /// <param name="deniedVariables">
    /// The environment variable names explicitly denied by the policy.
    /// </param>
    /// <returns>A defensive child environment policy snapshot.</returns>
    public static ChildEnvironmentPolicy CreateWithDenyListMode(
        IReadOnlySet<string> deniedVariables)
    {
        return new ChildEnvironmentPolicy
        {
            Mode = ChildEnvironmentMode.DenyList,
            DeniedVariables =
                CreateFrozenSet(
                    deniedVariables,
                    nameof(deniedVariables),
                    "Denied variables cannot be empty.")
        };
    }

    /// <summary>
    /// Creates an immutable ordinal snapshot of an environment-variable set.
    /// </summary>
    /// <param name="variables">
    /// The source environment-variable set.
    /// </param>
    /// <param name="parameterName">
    /// The parameter name used by validation exceptions.
    /// </param>
    /// <param name="emptyCollectionMessage">
    /// The exception message used when the source set is empty.
    /// </param>
    /// <returns>An immutable ordinal snapshot of the source set.</returns>
    private static FrozenSet<string> CreateFrozenSet(
        IReadOnlySet<string> variables,
        string parameterName,
        string emptyCollectionMessage)
    {
        ArgumentNullException.ThrowIfNull(
            variables,
            parameterName);

        if (variables.Count == 0)
        {
            throw new ArgumentException(
                emptyCollectionMessage,
                parameterName);
        }

        return variables.ToFrozenSet(
            StringComparer.Ordinal);
    }
}
