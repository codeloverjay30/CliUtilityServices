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
    /// environment-variable comparison semantics.
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

        IReadOnlyDictionary<string, string?> normalizedExplicitVariables =
            NormalizeVariables(
                explicitVariables,
                comparer,
                nameof(explicitVariables));

        IReadOnlyDictionary<string, string?> normalizedOverrides =
            NormalizeVariables(
                policy.Overrides,
                comparer,
                nameof(policy.Overrides));

        IReadOnlySet<string> normalizedAllowedVariables =
            NormalizeVariableNames(
                policy.AllowedVariables,
                comparer,
                nameof(policy.AllowedVariables));

        IReadOnlySet<string> normalizedAllowedInheritedVariables =
            NormalizeVariableNames(
                policy.AllowedInheritedVariables,
                comparer,
                nameof(policy.AllowedInheritedVariables));

        IReadOnlySet<string> normalizedDeniedVariables =
            NormalizeVariableNames(
                policy.DeniedVariables,
                comparer,
                nameof(policy.DeniedVariables));

        IReadOnlyDictionary<string, string?> normalizedParentEnvironment =
            NormalizeVariables(
                _environmentSource.GetEnvironmentVariables(),
                comparer,
                "parentEnvironment");

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
    /// The normalized allowed environment-variable names.
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
    /// The normalized source environment variables.
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
    /// Materializes and validates environment variables using authoritative
    /// operating-system comparison semantics.
    /// </summary>
    /// <param name="source">
    /// The environment variables to validate and materialize.
    /// </param>
    /// <param name="comparer">
    /// The operating-system-specific environment-variable comparer.
    /// </param>
    /// <param name="parameterName">
    /// The logical source name used in validation exceptions.
    /// </param>
    /// <returns>
    /// A validated dictionary using authoritative operating-system
    /// comparison semantics.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when a variable name or value is invalid.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when multiple variable names represent the same logical
    /// variable under the operating-system comparison semantics.
    /// </exception>
    private static IReadOnlyDictionary<string, string?>
        NormalizeVariables(
            IReadOnlyDictionary<string, string?> source,
            StringComparer comparer,
            string parameterName)
    {
        ArgumentNullException.ThrowIfNull(
            source,
            nameof(source));

        ArgumentNullException.ThrowIfNull(
            comparer,
            nameof(comparer));

        ArgumentException.ThrowIfNullOrWhiteSpace(
            parameterName,
            nameof(parameterName));

        var normalized =
            new Dictionary<string, string?>(
                comparer);

        foreach (KeyValuePair<string, string?> pair in source)
        {
            ValidateEnvironmentVariableName(
                pair.Key,
                parameterName);

            ValidateEnvironmentVariableValue(
                pair.Key,
                pair.Value,
                parameterName);

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
    /// Materializes and validates environment-variable names using
    /// authoritative operating-system comparison semantics.
    /// </summary>
    /// <param name="source">
    /// The environment-variable names to validate and materialize.
    /// </param>
    /// <param name="comparer">
    /// The operating-system-specific environment-variable comparer.
    /// </param>
    /// <param name="parameterName">
    /// The logical source name used in validation exceptions.
    /// </param>
    /// <returns>
    /// A validated set using authoritative operating-system
    /// comparison semantics.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when an environment-variable name is invalid.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when multiple names represent the same logical variable
    /// under the operating-system comparison semantics.
    /// </exception>
    private static IReadOnlySet<string>
        NormalizeVariableNames(
            IEnumerable<string> source,
            StringComparer comparer,
            string parameterName)
    {
        ArgumentNullException.ThrowIfNull(
            source,
            nameof(source));

        ArgumentNullException.ThrowIfNull(
            comparer,
            nameof(comparer));

        ArgumentException.ThrowIfNullOrWhiteSpace(
            parameterName,
            nameof(parameterName));

        var normalized =
            new HashSet<string>(
                comparer);

        foreach (string name in source)
        {
            ValidateEnvironmentVariableName(
                name,
                parameterName);

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
    /// Validates an environment-variable name at the execution boundary.
    /// </summary>
    /// <param name="name">
    /// The environment-variable name to validate.
    /// </param>
    /// <param name="parameterName">
    /// The logical source name used in the validation exception.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when the environment-variable name is empty, whitespace,
    /// contains an equals sign, or contains a null character.
    /// </exception>
    private static void ValidateEnvironmentVariableName(
        string? name,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Environment variable names cannot be null, empty, or whitespace.",
                parameterName);
        }

        ReadOnlySpan<char> nameSpan =
            name.AsSpan();

        if (nameSpan.Contains('='))
        {
            throw new ArgumentException(
                "Environment variable names cannot contain '='.",
                parameterName);
        }

        if (nameSpan.Contains('\0'))
        {
            throw new ArgumentException(
                "Environment variable names cannot contain null characters.",
                parameterName);
        }
    }

    /// <summary>
    /// Validates an environment-variable value at the execution boundary.
    /// </summary>
    /// <param name="name">
    /// The environment-variable name associated with the value.
    /// </param>
    /// <param name="value">
    /// The environment-variable value to validate.
    /// A null value is allowed and represents variable removal.
    /// </param>
    /// <param name="parameterName">
    /// The logical source name used in the validation exception.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when the environment-variable value contains a null character.
    /// </exception>
    private static void ValidateEnvironmentVariableValue(
        string name,
        string? value,
        string parameterName)
    {
        if (value is null)
        {
            return;
        }

        if (value.AsSpan().Contains('\0'))
        {
            throw new ArgumentException(
                $"Environment variable '{name}' cannot contain null characters in its value.",
                parameterName);
        }
    }

    /// <summary>
    /// Validates that all supplied environment variables are permitted
    /// by the configured allow-list.
    /// </summary>
    /// <param name="variables">
    /// The normalized variables to validate.
    /// </param>
    /// <param name="allowedVariables">
    /// The normalized allowed environment-variable names.
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
                    $"Environment variable '{name}' is not permitted by the configured allow-list.");
            }
        }
    }
}