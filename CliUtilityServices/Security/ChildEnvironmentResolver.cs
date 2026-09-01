using EnvironmentUtilityServices;

namespace CliUtilityServices.Security;

/// <summary>
/// Resolves child-process environment mutations according to an explicit
/// environment security policy.
/// </summary>
public sealed class ChildEnvironmentResolver
    : IChildEnvironmentResolver
{
    private readonly IProcessEnvironmentSource _environmentSource;
    private readonly IOsUtilityService _osUtilityService;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="ChildEnvironmentResolver"/> class.
    /// </summary>
    /// <param name="environmentSource">
    /// The parent-process environment source.
    /// </param>
    /// <param name="osUtilityService">
    /// The operating-system utility service used to determine authoritative
    /// environment-variable name comparison semantics.
    /// </param>
    public ChildEnvironmentResolver(
        IProcessEnvironmentSource environmentSource,
        IOsUtilityService osUtilityService)
    {
        ArgumentNullException.ThrowIfNull(
            environmentSource,
            nameof(environmentSource));

        ArgumentNullException.ThrowIfNull(
            osUtilityService,
            nameof(osUtilityService));

        _environmentSource = environmentSource;
        _osUtilityService = osUtilityService;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string?> Resolve(
        ChildEnvironmentPolicy policy,
        IReadOnlyDictionary<string, string?> explicitVariables)
    {
        ArgumentNullException.ThrowIfNull(
            policy,
            nameof(policy));

        ArgumentNullException.ThrowIfNull(
            explicitVariables,
            nameof(explicitVariables));

        StringComparer comparer =
            _osUtilityService.GetComparer();

        /*
         * The resolver is the authoritative execution boundary.
         * Never rely on comparers supplied by caller-owned collections.
         */
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

        IReadOnlySet<string> normalizedAllowedInheritedVariables =
            NormalizeVariableNames(
                policy.AllowedInheritedVariables,
                comparer);

        IReadOnlySet<string> normalizedDeniedVariables =
            NormalizeVariableNames(
                policy.DeniedVariables,
                comparer);

        IReadOnlyDictionary<string, string?> normalizedParentEnvironment =
            NormalizeVariables(
                _environmentSource.GetEnvironmentVariables(),
                comparer);

        var mutations =
            new Dictionary<string, string?>(
                comparer);

        switch (policy.Mode)
        {
            case ChildEnvironmentMode.InheritAll:
                break;

            case ChildEnvironmentMode.DenyList:
                break;

            case ChildEnvironmentMode.AllowInheritedList:
                RemoveVariablesOutsideAllowList(
                    normalizedParentEnvironment,
                    normalizedAllowedInheritedVariables,
                    mutations);
                break;

            case ChildEnvironmentMode.AllowList:
                RemoveVariablesOutsideAllowList(
                    normalizedParentEnvironment,
                    normalizedAllowedVariables,
                    mutations);

                ValidateVariablesAgainstAllowList(
                    normalizedExplicitVariables,
                    normalizedAllowedVariables);

                ValidateVariablesAgainstAllowList(
                    normalizedOverrides,
                    normalizedAllowedVariables);
                break;

            case ChildEnvironmentMode.Isolated:
                RemoveAllParentVariables(
                    normalizedParentEnvironment,
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
            normalizedOverrides);

        ApplyOverrides(
            mutations,
            normalizedExplicitVariables);

        ApplyDenyList(
            mutations,
            normalizedDeniedVariables);

        return mutations;
    }

    /// <summary>
    /// Marks every parent environment variable for removal.
    /// </summary>
    /// <param name="parentEnvironment">
    /// The normalized parent environment.
    /// </param>
    /// <param name="mutations">
    /// The destination mutation dictionary.
    /// </param>
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
    /// <param name="parentEnvironment">
    /// The normalized parent environment.
    /// </param>
    /// <param name="allowedVariables">
    /// The normalized set of allowed environment-variable names.
    /// </param>
    /// <param name="mutations">
    /// The destination mutation dictionary.
    /// </param>
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
    /// Applies explicit environment-variable overrides.
    /// </summary>
    /// <param name="destination">
    /// The destination mutation dictionary.
    /// </param>
    /// <param name="source">
    /// The normalized source variables.
    /// </param>
    private static void ApplyOverrides(
        IDictionary<string, string?> destination,
        IReadOnlyDictionary<string, string?> source)
    {
        foreach (KeyValuePair<string, string?> variable in source)
        {
            destination[variable.Key] =
                variable.Value;
        }
    }

    /// <summary>
    /// Applies environment-variable denial as the final security boundary.
    /// </summary>
    /// <param name="environment">
    /// The destination mutation dictionary.
    /// </param>
    /// <param name="deniedVariables">
    /// The normalized denied environment-variable names.
    /// </param>
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
    /// Materializes environment variables using authoritative
    /// operating-system comparison semantics.
    /// </summary>
    /// <param name="source">
    /// The environment variables to materialize.
    /// </param>
    /// <param name="comparer">
    /// The operating-system-specific variable-name comparer.
    /// </param>
    /// <returns>
    /// A dictionary using authoritative operating-system comparison semantics.
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
        ArgumentNullException.ThrowIfNull(
            source,
            nameof(source));

        ArgumentNullException.ThrowIfNull(
            comparer,
            nameof(comparer));

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
    /// Materializes environment-variable names using authoritative
    /// operating-system comparison semantics.
    /// </summary>
    /// <param name="source">
    /// The environment-variable names to materialize.
    /// </param>
    /// <param name="comparer">
    /// The operating-system-specific variable-name comparer.
    /// </param>
    /// <returns>
    /// A set using authoritative operating-system comparison semantics.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when multiple names represent the same logical environment
    /// variable under the specified comparison semantics.
    /// </exception>
    private static IReadOnlySet<string>
        NormalizeVariableNames(
            IEnumerable<string> source,
            StringComparer comparer)
    {
        ArgumentNullException.ThrowIfNull(
            source,
            nameof(source));

        ArgumentNullException.ThrowIfNull(
            comparer,
            nameof(comparer));

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
    /// Validates that all supplied environment variables are permitted
    /// by the configured allow-list.
    /// </summary>
    /// <param name="variables">
    /// The normalized variables to validate.
    /// </param>
    /// <param name="allowedVariables">
    /// The normalized allowed variable names.
    /// </param>
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
                    $"Environment variable '{name}' is not permitted by " +
                    "the configured allow-list.");
            }
        }
    }
}