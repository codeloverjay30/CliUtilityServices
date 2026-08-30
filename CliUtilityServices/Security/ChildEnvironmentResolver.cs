using EnvironmentUtilityServices;

namespace CliUtilityServices.Security;

/// <summary>
/// Resolves child-process environment mutations according to an explicit
/// environment security policy.
/// </summary>
public sealed class ChildEnvironmentResolver : IChildEnvironmentResolver
{
    private readonly IProcessEnvironmentSource _environmentSource;
    private readonly IOsUtilityService _osUtilityService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChildEnvironmentResolver"/> class.
    /// </summary>
    /// <param name="environmentSource">
    /// The parent-process environment source.
    /// </param>
    public ChildEnvironmentResolver(
        IProcessEnvironmentSource environmentSource,
        IOsUtilityService osUtilityService
    )
    {
        ArgumentNullException.ThrowIfNull(environmentSource,nameof(environmentSource));
        ArgumentNullException.ThrowIfNull(osUtilityService, nameof(osUtilityService));
        _osUtilityService = osUtilityService;
        _environmentSource = environmentSource;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string?> Resolve(
        ChildEnvironmentPolicy policy,
        IReadOnlyDictionary<string, string?> explicitVariables)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(explicitVariables);

        StringComparer comparer = _osUtilityService.GetComparer();

        IReadOnlyDictionary<string, string?> normalizedExplicitVariables =
            NormalizeVariables(
                explicitVariables,
                comparer);

        IReadOnlyDictionary<string, string?> normalizedOverrides =
        NormalizeVariables(
            policy.Overrides,
            comparer);

        IReadOnlySet<string> normalizedAllowedVariables =
            NormalizeVariableNames(
                policy.AllowedVariables,
                comparer);
            
        IReadOnlyDictionary<string, string?> parentEnvironment =
            _environmentSource.GetEnvironmentVariables();

        var mutations =
            new Dictionary<string, string?>(comparer);

        switch (policy.Mode)
        {
            case ChildEnvironmentMode.InheritAll:
                break;

            case ChildEnvironmentMode.DenyList:
                break;

            case ChildEnvironmentMode.AllowInheritedList:
                RemoveVariablesOutsideAllowList(
                    parentEnvironment,
                    policy.AllowedInheritedVariables,
                    mutations);
                break;

            case ChildEnvironmentMode.AllowList:
                    RemoveVariablesOutsideAllowList(
                        parentEnvironment,
                        policy.AllowedVariables,
                        mutations);

                    ValidateVariablesAgainstAllowList(
                        explicitVariables,
                        policy.AllowedVariables);

                    ValidateVariablesAgainstAllowList(
                        policy.Overrides,
                        policy.AllowedVariables);
                break;

            case ChildEnvironmentMode.Isolated:
                RemoveAllParentVariables(
                    parentEnvironment,
                    mutations);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(policy.Mode),
                    policy.Mode,
                    "Unsupported child environment mode.");
        }

        ApplyOverrides(
            mutations,
            policy.Overrides);

        ApplyOverrides(
            mutations,
            explicitVariables);

        ApplyDenyList(
            mutations,
            policy.DeniedVariables);

        return mutations;
    }

    /// <summary>
    /// Marks every parent environment variable for removal.
    /// </summary>
    private static void RemoveAllParentVariables(
        IReadOnlyDictionary<string, string?> parentEnvironment,
        IDictionary<string, string?> mutations)
    {
        foreach (string name in parentEnvironment.Keys)
        {
            mutations[name] = null;
        }
    }

    /// <summary>
    /// Marks parent environment variables outside the allow-list for removal.
    /// </summary>
    private static void RemoveVariablesOutsideAllowList(
        IReadOnlyDictionary<string, string?> parentEnvironment,
        IReadOnlySet<string> allowedVariables,
        IDictionary<string, string?> mutations)
    {
        foreach (string name in parentEnvironment.Keys)
        {
            if (!allowedVariables.Contains(name))
            {
                mutations[name] = null;
            }
        }
    }

    /// <summary>
    /// Applies explicit environment variable overrides.
    /// </summary>
    private static void ApplyOverrides(
        IDictionary<string, string?> destination,
        IReadOnlyDictionary<string, string?> source)
    {
        foreach (KeyValuePair<string, string?> variable in source)
        {
            destination[variable.Key] = variable.Value;
        }
    }

    /// <summary>
    /// Applies environment-variable denial as the final security boundary.
    /// </summary>
    private static void ApplyDenyList(
        IDictionary<string, string?> environment,
        IReadOnlySet<string> deniedVariables)
    {
        foreach (string deniedVariable in deniedVariables)
        {
            environment[deniedVariable] = null;
        }
    }

    /// <summary>
    /// Normalizes environment variables using the specified
    /// operating-system comparison semantics.
    /// </summary>
    /// <param name="source">
    /// The environment variables to normalize.
    /// </param>
    /// <param name="comparer">
    /// The operating-system-specific variable-name comparer.
    /// </param>
    /// <returns>
    /// A normalized environment variable dictionary.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when multiple variable names represent the same logical
    /// environment variable under the specified comparison semantics.
    /// </exception>
    private static IReadOnlyDictionary<string, string?>
        NormalizeVariables(
            IReadOnlyDictionary<string, string?> source,
            StringComparer comparer)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(comparer);

        var normalized =
            new Dictionary<string, string?>(
                comparer);

        foreach (KeyValuePair<string, string?> pair in source)
        {
            if (!normalized.TryAdd(
                    pair.Key,
                    pair.Value))
            {
                throw new InvalidOperationException(
                    $"Environment variable '{pair.Key}' conflicts with another " +
                    "variable under the current operating-system comparison rules.");
            }
        }

        return normalized;
    }

    /// <summary>
    /// Normalizes environment variable names using the specified
    /// operating-system comparison semantics.
    /// </summary>
    /// <param name="source">
    /// The environment variable names to normalize.
    /// </param>
    /// <param name="comparer">
    /// The operating-system-specific variable-name comparer.
    /// </param>
    /// <returns>
    /// A normalized set of environment variable names.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when multiple names represent the same logical
    /// environment variable under the specified comparison semantics.
    /// </exception>
    private static IReadOnlySet<string>
        NormalizeVariableNames(
            IEnumerable<string> source,
            StringComparer comparer)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(comparer);

        var normalized =
            new HashSet<string>(
                comparer);

        foreach (string name in source)
        {
            if (!normalized.Add(name))
            {
                throw new InvalidOperationException(
                    $"Environment variable '{name}' conflicts with another " +
                    "variable under the current operating-system comparison rules.");
            }
        }

        return normalized;
    }


    /// <summary>
    /// Validates that explicitly supplied variables are permitted by
    /// the configured allow-list.
    /// </summary>
    private static void ValidateExplicitVariablesAgainstAllowList(
        IReadOnlyDictionary<string, string?> explicitVariables,
        IReadOnlySet<string> allowedVariables)
    {
        foreach (string name in explicitVariables.Keys)
        {
            if (!allowedVariables.Contains(name))
            {
                throw new InvalidOperationException(
                    $"Environment variable '{name}' is not permitted by the configured allow-list.");
            }
        }
    }

    /// <summary>
    /// Validates that all supplied environment variables are permitted
    /// by the configured allow-list.
    /// </summary>
    /// <param name="variables">The variables to validate.</param>
    /// <param name="allowedVariables">The allowed variable names.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a variable is not permitted by the allow-list.
    /// </exception>
    private static void ValidateVariablesAgainstAllowList(
        IReadOnlyDictionary<string, string?> variables,
        IReadOnlySet<string> allowedVariables)
    {
        foreach (string name in variables.Keys)
        {
            if (!allowedVariables.Contains(name))
            {
                throw new InvalidOperationException(
                    $"Environment variable '{name}' is not permitted by the configured allow-list.");
            }
        }
    }

}