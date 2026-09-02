using System.Collections.Frozen;

namespace CliUtilityServices.Security;

/// <summary>
/// Defines the environment inheritance policy for a child process.
/// </summary>
public sealed record ChildEnvironmentPolicy
{
    private static readonly FrozenSet<string> EmptyVariableSet =
        Array.Empty<string>()
            .ToFrozenSet(
                StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, string?> EmptyOverrides =
        Array.Empty<KeyValuePair<string, string?>>()
            .ToFrozenDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal);

    private IReadOnlySet<string> _allowedInheritedVariables =
        EmptyVariableSet;

    private IReadOnlySet<string> _allowedVariables =
        EmptyVariableSet;

    private IReadOnlySet<string> _deniedVariables =
        EmptyVariableSet;

    private IReadOnlyDictionary<string, string?> _overrides =
        EmptyOverrides;

    /// <summary>
    /// Gets the environment inheritance mode.
    /// </summary>
    public ChildEnvironmentMode Mode { get; init; }
        = ChildEnvironmentMode.InheritAll;

    /// <summary>
    /// Gets the parent environment variable names that may be inherited
    /// when allow-list mode is enabled.
    /// </summary>
    public IReadOnlySet<string> AllowedInheritedVariables
    {
        get =>
            _allowedInheritedVariables;

        init
        {
            ArgumentNullException.ThrowIfNull(
                value);

            _allowedInheritedVariables =
                value.ToFrozenSet(
                    StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Gets the allowed environment variable names
    /// when allow-list mode is enabled.
    /// </summary>
    public IReadOnlySet<string> AllowedVariables
    {
        get =>
            _allowedVariables;

        init
        {
            ArgumentNullException.ThrowIfNull(
                value);

            _allowedVariables =
                value.ToFrozenSet(
                    StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Gets the denied environment variable names
    /// when deny-list mode is enabled.
    /// </summary>
    public IReadOnlySet<string> DeniedVariables
    {
        get =>
            _deniedVariables;

        init
        {
            ArgumentNullException.ThrowIfNull(
                value);

            _deniedVariables =
                value.ToFrozenSet(
                    StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Gets explicit environment variable overrides for the child process.
    /// </summary>
    public IReadOnlyDictionary<string, string?> Overrides
    {
        get =>
            _overrides;

        init
        {
            ArgumentNullException.ThrowIfNull(
                value);

            _overrides =
                value.ToFrozenDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.Ordinal);
        }
    }
}