namespace CliUtilityServices.Security;

public class ChildEnvironmentPolicyBuilder
{
    public static ChildEnvironmentPolicy CreateWithAllowListMode(
        IReadOnlySet<string> environmentVariables
    )
    {
        ArgumentNullException.ThrowIfNull(environmentVariables, nameof(environmentVariables));
        if (environmentVariables.Count == 0)
        {
            throw new ArgumentException(
                "Environment variables cannot be empty.",
                nameof(environmentVariables));
        }

        ChildEnvironmentPolicy childEnvironmentPolicy = new()
        {
            Mode = ChildEnvironmentMode.AllowList,
            AllowedVariables = environmentVariables,
        };

        return childEnvironmentPolicy;
    }

    public static ChildEnvironmentPolicy CreateWithAllowInheritedListMode(
        IReadOnlySet<string> allowedInheritedVariables
    )
    {
        ArgumentNullException.ThrowIfNull(allowedInheritedVariables, nameof(allowedInheritedVariables));
        if (allowedInheritedVariables.Count == 0)
        {
            throw new ArgumentException(
                "Allowed inherited variables cannot be empty.",
                nameof(allowedInheritedVariables));
        }

        ChildEnvironmentPolicy childEnvironmentPolicy = new()
        {
            Mode = ChildEnvironmentMode.AllowInheritedList,
            AllowedInheritedVariables = allowedInheritedVariables,
        };

        return childEnvironmentPolicy;
    }

    public static ChildEnvironmentPolicy CreateWithAllowInheritedListMode(
        IReadOnlySet<string> allowedInheritedVariables,
        IReadOnlySet<string> deniedVariables
    )
    {
        ArgumentNullException.ThrowIfNull(allowedInheritedVariables, nameof(allowedInheritedVariables));
        if (allowedInheritedVariables.Count == 0)
        {
            throw new ArgumentException(
                "Allowed inherited variables cannot be empty.",
                nameof(allowedInheritedVariables));
        }

        ArgumentNullException.ThrowIfNull(deniedVariables, nameof(deniedVariables));
        if (deniedVariables.Count == 0)
        {
            throw new ArgumentException(
                "Denied variables cannot be empty.",
                nameof(deniedVariables));
        }

        ChildEnvironmentPolicy childEnvironmentPolicy = new()
        {
            Mode = ChildEnvironmentMode.AllowInheritedList,
            AllowedInheritedVariables = allowedInheritedVariables,
            DeniedVariables = deniedVariables,
        };

        return childEnvironmentPolicy;
    }
    
    
    public static ChildEnvironmentPolicy CreateWithDenyListMode(
        IReadOnlySet<string> deniedVariables
    )
    {
        ArgumentNullException.ThrowIfNull(deniedVariables, nameof(deniedVariables));
        if (deniedVariables.Count == 0)
        {
            throw new ArgumentException(
                "Denied variables cannot be empty.",
                nameof(deniedVariables));
        }

        ChildEnvironmentPolicy childEnvironmentPolicy = new()
        {
            Mode = ChildEnvironmentMode.DenyList,
            DeniedVariables = deniedVariables,
        };

        return childEnvironmentPolicy;
    }
}
