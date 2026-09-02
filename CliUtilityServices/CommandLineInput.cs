using System.Collections.Frozen;
using System.ComponentModel;
using System.Text;
using CliUtilityServices.Pipes;
using CliUtilityServices.Security;
using CliWrap;
using EnvironmentUtilityServices;

namespace CliUtilityServices;

/// <summary>
/// Represents the configuration required to execute a command-line process.
/// </summary>
public record class CommandLineInput
{
    private static readonly FrozenDictionary<string, string?>
        EmptyEnvironmentVariables =
            Array.Empty<KeyValuePair<string, string?>>()
                .ToFrozenDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.Ordinal);

    private readonly Encoding? _inputEncoding;
    private readonly Encoding? _outputEncoding;
    private Encoding? _defaultEncoding;

    private IReadOnlyList<string> _arguments =
        Array.Empty<string>();

    private IReadOnlyDictionary<string, string?> _environmentVariables =
        EmptyEnvironmentVariables;

    /// <summary>
    /// Gets the strategy used to capture command output.
    /// </summary>
    public ICommandPipeStrategy PipeStrategy { get; init; }
        = new SlidingWindowPipeStrategy(500);

    /// <summary>
    /// Gets the executable or command name to execute.
    /// </summary>
    public required string Command { get; init; }

    /// <summary>
    /// Gets an immutable snapshot of the arguments passed to the executable.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the assigned argument collection is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the assigned argument collection contains a null value.
    /// </exception>
    public IEnumerable<string> Arguments
    {
        get =>
            _arguments;

        init
        {
            ArgumentNullException.ThrowIfNull(
                value);

            string[] materializedArguments =
                value.ToArray();

            ReadOnlySpan<string> argumentsSpan =
                materializedArguments;

            for (int index = 0; index < argumentsSpan.Length; index++)
            {
                if (argumentsSpan[index] is null)
                {
                    throw new ArgumentException(
                        "Command-line arguments cannot contain null values.",
                        nameof(value));
                }
            }

            _arguments =
                Array.AsReadOnly(
                    materializedArguments);
        }
    }

    /// <summary>
    /// Gets the working directory used by the child process.
    /// </summary>
    public string WorkingDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Gets the command result validation strategy.
    /// </summary>
    public CommandResultValidation Validation { get; init; }
        = CommandResultValidation.ZeroExitCode;

    /// <summary>
    /// Gets the maximum execution duration.
    /// A null value disables the internal timeout.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Gets environment variables explicitly provided to the child process.
    /// </summary>
    public IReadOnlyDictionary<string, string?> EnvironmentVariables
    {
        get =>
            _environmentVariables;

        init
        {
            ArgumentNullException.ThrowIfNull(
                value);

            _environmentVariables =
                value.ToFrozenDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.Ordinal);
        }
    }

    /// <summary>
    /// Gets the environment policy that controls which environment variables
    /// the child process may inherit or receive.
    /// </summary>
    public ChildEnvironmentPolicy EnvironmentPolicy { get; init; } = ChildEnvironmentPolicies.Compatible;

    /// <summary>
    /// Gets the encoding used for standard input.
    /// </summary>
    public Encoding InputEncoding
    {
        get => _inputEncoding ?? DefaultEncoding;
        init => _inputEncoding = value;
    }

    /// <summary>
    /// Gets the encoding used for standard output and standard error.
    /// </summary>
    public Encoding OutputEncoding
    {
        get => _outputEncoding ?? DefaultEncoding;
        init => _outputEncoding = value;
    }

    /// <summary>
    /// Gets the default encoding used by the process.
    /// </summary>
    public Encoding DefaultEncoding
    {
        get => _defaultEncoding ?? FallbackEncoding;
        init
        {
            value ??= FallbackEncoding;
            _defaultEncoding = value;
        }
    }

    /// <summary>
    /// Gets the legacy environment service associated with this request.
    /// </summary>
    /// <remarks>
    /// This property is retained only for source compatibility.
    /// The current execution pipeline does not use this value for
    /// operating-system or security decisions.
    /// </remarks>
    [Obsolete(
        "EnvironmentService is retained for compatibility only. " +
        "Platform decisions are performed by CliCommandExecutor.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public IEnvironmentService? EnvironmentService
    {
        get;
        init;
    }

    /// <summary>
    /// Gets the fallback encoding used when no explicit encoding is configured.
    /// </summary>
    public Encoding FallbackEncoding => Encoding.UTF8;
}