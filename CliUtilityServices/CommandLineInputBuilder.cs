using System.Text;
using CliUtilityServices.Pipes;
using CliUtilityServices.Security;
using CliWrap;
using EnvironmentUtilityServices;

namespace CliUtilityServices;

/// <summary>
/// Provides a fluent and defensive builder for creating
/// <see cref="CommandLineInput"/> instances.
/// </summary>
public sealed class CommandLineInputBuilder
{
    private Encoding? _inputEncoding;
    private Encoding? _outputEncoding;
    private Encoding? _defaultEncoding;

    private ICommandPipeStrategy _pipeStrategy =
        new SlidingWindowPipeStrategy(500);

    private string _command = string.Empty;

    private IReadOnlyList<string> _arguments =
        Array.Empty<string>();

    private string _workingDirectory = string.Empty;

    private CommandResultValidation _validation =
        CommandResultValidation.ZeroExitCode;

    private IEnvironmentService? _environmentService;

    private TimeSpan? _timeout;

    private IReadOnlyDictionary<string, string?> _environmentVariables =
        new Dictionary<string, string?>();

    private ChildEnvironmentPolicy _environmentPolicy =
        new()
        {
            Mode = ChildEnvironmentMode.InheritAll
        };

    /// <summary>
    /// Initializes static encoding support required by the builder.
    /// </summary>
    static CommandLineInputBuilder()
    {
        Encoding.RegisterProvider(
            CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// Configures the executable or command to execute.
    /// </summary>
    /// <param name="command">
    /// The executable name or executable path.
    /// </param>
    /// <returns>The current builder instance.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the command is null, empty, or whitespace.
    /// </exception>
    public CommandLineInputBuilder WithCommand(
        string command)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            command,
            nameof(command));

        _command = command;

        return this;
    }

    /// <summary>
    /// Replaces all command-line arguments.
    /// </summary>
    /// <param name="arguments">
    /// The arguments to pass to the executable.
    /// </param>
    /// <returns>The current builder instance.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the argument collection is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the argument collection contains a null value.
    /// </exception>
    public CommandLineInputBuilder WithArguments(
        IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            arguments,
            nameof(arguments));

        string[] materializedArguments =
            arguments.ToArray();

        ValidateArguments(
            materializedArguments,
            nameof(arguments));

        _arguments = materializedArguments;

        return this;
    }

    /// <summary>
    /// Adds a single command-line argument.
    /// </summary>
    /// <param name="argument">
    /// The argument to append.
    /// </param>
    /// <returns>The current builder instance.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the argument is null.
    /// </exception>
    public CommandLineInputBuilder AddArgument(
        string argument)
    {
        ArgumentNullException.ThrowIfNull(
            argument,
            nameof(argument));

        _arguments = _arguments
            .Append(argument)
            .ToArray();

        return this;
    }

    /// <summary>
    /// Adds multiple command-line arguments.
    /// </summary>
    /// <param name="arguments">
    /// The arguments to append.
    /// </param>
    /// <returns>The current builder instance.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when the argument collection is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when the argument collection contains a null value.
    /// </exception>
    public CommandLineInputBuilder AddArguments(
        IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(
            arguments,
            nameof(arguments));

        string[] materializedArguments =
            arguments.ToArray();

        ValidateArguments(
            materializedArguments,
            nameof(arguments));

        _arguments = _arguments
            .Concat(materializedArguments)
            .ToArray();

        return this;
    }

    /// <summary>
    /// Configures the standard-input encoding.
    /// </summary>
    /// <param name="inputEncoding">
    /// The encoding used for standard input.
    /// </param>
    /// <returns>The current builder instance.</returns>
    public CommandLineInputBuilder WithInputEncoding(
        Encoding inputEncoding)
    {
        ArgumentNullException.ThrowIfNull(
            inputEncoding,
            nameof(inputEncoding));

        _inputEncoding = inputEncoding;

        return this;
    }

    /// <summary>
    /// Configures the standard-output and standard-error encoding.
    /// </summary>
    /// <param name="outputEncoding">
    /// The encoding used to decode process output.
    /// </param>
    /// <returns>The current builder instance.</returns>
    public CommandLineInputBuilder WithOutputEncoding(
        Encoding outputEncoding)
    {
        ArgumentNullException.ThrowIfNull(
            outputEncoding,
            nameof(outputEncoding));

        _outputEncoding = outputEncoding;

        return this;
    }

    /// <summary>
    /// Configures the default process encoding.
    /// </summary>
    /// <param name="defaultEncoding">
    /// The default encoding, or null to use the platform fallback.
    /// </param>
    /// <returns>The current builder instance.</returns>
    public CommandLineInputBuilder WithDefaultEncoding(
        Encoding? defaultEncoding)
    {
        _defaultEncoding = defaultEncoding;

        return this;
    }

    /// <summary>
    /// Configures the strategy used to capture process output.
    /// </summary>
    /// <param name="pipeStrategy">
    /// The command pipe strategy.
    /// </param>
    /// <returns>The current builder instance.</returns>
    public CommandLineInputBuilder WithPipeStrategy(
        ICommandPipeStrategy pipeStrategy)
    {
        ArgumentNullException.ThrowIfNull(
            pipeStrategy,
            nameof(pipeStrategy));

        _pipeStrategy = pipeStrategy;

        return this;
    }

    /// <summary>
    /// Configures the child process working directory.
    /// </summary>
    /// <param name="workingDirectory">
    /// The working directory path.
    /// </param>
    /// <returns>The current builder instance.</returns>
    public CommandLineInputBuilder WithWorkingDirectory(
        string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(
            workingDirectory,
            nameof(workingDirectory));

        _workingDirectory = workingDirectory;

        return this;
    }

    /// <summary>
    /// Configures the CliWrap command-result validation behavior.
    /// </summary>
    /// <param name="validation">
    /// The command-result validation strategy.
    /// </param>
    /// <returns>The current builder instance.</returns>
    public CommandLineInputBuilder WithValidation(
        CommandResultValidation validation)
    {
        _validation = validation;

        return this;
    }

    /// <summary>
    /// Configures the environment service used for
    /// platform-specific behavior.
    /// </summary>
    /// <param name="environmentService">
    /// The environment service.
    /// </param>
    /// <returns>The current builder instance.</returns>
    public CommandLineInputBuilder WithEnvironmentService(
        IEnvironmentService environmentService)
    {
        ArgumentNullException.ThrowIfNull(
            environmentService,
            nameof(environmentService));

        _environmentService = environmentService;

        return this;
    }

    /// <summary>
    /// Configures the maximum execution duration.
    /// </summary>
    /// <param name="timeout">
    /// The maximum allowed execution duration.
    /// </param>
    /// <returns>The current builder instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the timeout is zero or negative.
    /// </exception>
    public CommandLineInputBuilder WithTimeout(
        TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                timeout,
                "Timeout must be greater than zero.");
        }

        _timeout = timeout;

        return this;
    }

    /// <summary>
    /// Removes the internally configured execution timeout.
    /// </summary>
    /// <returns>The current builder instance.</returns>
    public CommandLineInputBuilder WithoutTimeout()
    {
        _timeout = null;

        return this;
    }

    /// <summary>
    /// Configures explicit environment variables supplied
    /// to the child process.
    /// </summary>
    /// <param name="environmentVariables">
    /// The environment variables supplied to the child process.
    /// </param>
    /// <returns>The current builder instance.</returns>
    public CommandLineInputBuilder WithEnvironmentVariables(
        IReadOnlyDictionary<string, string?> environmentVariables)
    {
        ArgumentNullException.ThrowIfNull(
            environmentVariables,
            nameof(environmentVariables));

        ValidateEnvironmentVariables(
            environmentVariables,
            nameof(environmentVariables));

        _environmentVariables =
            new Dictionary<string, string?>(
                environmentVariables,
                GetEnvironmentVariableComparer());

        return this;
    }

    /// <summary>
    /// Adds or replaces an explicit child-process environment variable.
    /// </summary>
    /// <param name="name">
    /// The environment variable name.
    /// </param>
    /// <param name="value">
    /// The environment variable value.
    /// A null value may be interpreted by the execution layer
    /// as a request to remove the variable.
    /// </param>
    /// <returns>The current builder instance.</returns>
    public CommandLineInputBuilder AddEnvironmentVariable(
        string name,
        string? value)
    {
        ValidateEnvironmentVariableName(
            name,
            nameof(name));

        var variables =
            new Dictionary<string, string?>(
                _environmentVariables,
                GetEnvironmentVariableComparer())
            {
                [name] = value
            };

        _environmentVariables = variables;

        return this;
    }

    /// <summary>
    /// Adds or replaces multiple explicit child-process
    /// environment variables.
    /// </summary>
    /// <param name="environmentVariables">
    /// The environment variables to add or replace.
    /// </param>
    /// <returns>The current builder instance.</returns>
    public CommandLineInputBuilder AddEnvironmentVariables(
        IReadOnlyDictionary<string, string?> environmentVariables)
    {
        ArgumentNullException.ThrowIfNull(
            environmentVariables,
            nameof(environmentVariables));

        ValidateEnvironmentVariables(
            environmentVariables,
            nameof(environmentVariables));

        var variables =
            new Dictionary<string, string?>(
                _environmentVariables,
                GetEnvironmentVariableComparer());

        foreach (KeyValuePair<string, string?> pair
                 in environmentVariables)
        {
            variables[pair.Key] = pair.Value;
        }

        _environmentVariables = variables;

        return this;
    }

    /// <summary>
    /// Clears all explicitly configured child-process
    /// environment variables.
    /// </summary>
    /// <returns>The current builder instance.</returns>
    public CommandLineInputBuilder ClearEnvironmentVariables()
    {
        _environmentVariables =
            new Dictionary<string, string?>(
                GetEnvironmentVariableComparer());

        return this;
    }

    /// <summary>
    /// Configures the child-process environment security policy.
    /// </summary>
    /// <param name="environmentPolicy">
    /// The environment inheritance and filtering policy.
    /// </param>
    /// <returns>The current builder instance.</returns>
    public CommandLineInputBuilder WithEnvironmentPolicy(
        ChildEnvironmentPolicy environmentPolicy)
    {
        ArgumentNullException.ThrowIfNull(
            environmentPolicy,
            nameof(environmentPolicy));

        ValidateEnvironmentPolicy(environmentPolicy);

        _environmentPolicy =
            CloneEnvironmentPolicy(environmentPolicy);

        return this;
    }

    /// <summary>
    /// Configures the child process to inherit all parent
    /// environment variables.
    /// </summary>
    /// <returns>The current builder instance.</returns>
    public CommandLineInputBuilder WithInheritedEnvironment()
    {
        _environmentPolicy = new ChildEnvironmentPolicy
        {
            Mode = ChildEnvironmentMode.InheritAll
        };

        return this;
    }

    /// <summary>
    /// Configures the child process to avoid intentional
    /// parent-environment inheritance.
    /// </summary>
    /// <returns>The current builder instance.</returns>
    public CommandLineInputBuilder WithIsolatedEnvironment()
    {
        _environmentPolicy = new ChildEnvironmentPolicy
        {
            Mode = ChildEnvironmentMode.Isolated
        };

        return this;
    }

    /// <summary>
    /// Configures an allow-list of parent environment variables
    /// that may be inherited by the child process.
    /// </summary>
    /// <param name="allowedInheritedVariables">
    /// The allowed inherited environment variable names.
    /// </param>
    /// <returns>The current builder instance.</returns>
    public CommandLineInputBuilder WithAllowedInheritedEnvironmentVariables(
        IReadOnlySet<string> allowedInheritedVariables)
    {
        ArgumentNullException.ThrowIfNull(
            allowedInheritedVariables,
            nameof(allowedInheritedVariables));

        ValidateEnvironmentVariableNames(
            allowedInheritedVariables,
            nameof(allowedInheritedVariables),
            requireAtLeastOne: true);

        _environmentPolicy = new ChildEnvironmentPolicy
        {
            Mode = ChildEnvironmentMode.AllowInheritedList,
            AllowedInheritedVariables =
                CopyEnvironmentVariableSet(
                    allowedInheritedVariables)
        };

        return this;
    }

    /// <summary>
    /// Configures an allow-list of environment variable names
    /// accepted by the child environment policy.
    /// </summary>
    /// <param name="allowedVariables">
    /// The allowed environment variable names.
    /// </param>
    /// <returns>The current builder instance.</returns>
    public CommandLineInputBuilder WithAllowedEnvironmentVariables(
        IReadOnlySet<string> allowedVariables)
    {
        ArgumentNullException.ThrowIfNull(
            allowedVariables,
            nameof(allowedVariables));

        ValidateEnvironmentVariableNames(
            allowedVariables,
            nameof(allowedVariables),
            requireAtLeastOne: true);

        _environmentPolicy = new ChildEnvironmentPolicy
        {
            Mode = ChildEnvironmentMode.AllowList,
            AllowedVariables =
                CopyEnvironmentVariableSet(
                    allowedVariables)
        };

        return this;
    }

    /// <summary>
    /// Configures an explicit deny-list for child-process
    /// environment variables.
    /// </summary>
    /// <remarks>
    /// A deny-list should not be treated as the only security boundary
    /// because unknown sensitive variables cannot be enumerated reliably.
    /// </remarks>
    /// <param name="deniedVariables">
    /// The denied environment variable names.
    /// </param>
    /// <returns>The current builder instance.</returns>
    public CommandLineInputBuilder WithDeniedEnvironmentVariables(
        IReadOnlySet<string> deniedVariables)
    {
        ArgumentNullException.ThrowIfNull(
            deniedVariables,
            nameof(deniedVariables));

        ValidateEnvironmentVariableNames(
            deniedVariables,
            nameof(deniedVariables),
            requireAtLeastOne: true);

        _environmentPolicy = new ChildEnvironmentPolicy
        {
            Mode = ChildEnvironmentMode.DenyList,
            DeniedVariables =
                CopyEnvironmentVariableSet(
                    deniedVariables)
        };

        return this;
    }

    /// <summary>
    /// Builds an immutable command-line input instance.
    /// </summary>
    /// <returns>The configured command-line input.</returns>
    public CommandLineInput Build()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            _command,
            nameof(_command));

        ArgumentNullException.ThrowIfNull(
            _environmentService,
            nameof(_environmentService));

        ArgumentNullException.ThrowIfNull(
            _pipeStrategy,
            nameof(_pipeStrategy));

        ArgumentNullException.ThrowIfNull(
            _environmentPolicy,
            nameof(_environmentPolicy));

        if (_timeout is { } timeout &&
            timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(_timeout),
                timeout,
                "Timeout must be greater than zero.");
        }

        ValidateArguments(
            _arguments,
            nameof(_arguments));

        ValidateEnvironmentVariables(
            _environmentVariables,
            nameof(_environmentVariables));

        ValidateEnvironmentPolicy(
            _environmentPolicy);

        return new CommandLineInput
        {
            PipeStrategy = _pipeStrategy,
            Command = _command,
            Arguments = _arguments.ToArray(),
            WorkingDirectory = _workingDirectory,
            Validation = _validation,
            Timeout = _timeout,

            EnvironmentVariables =
                new Dictionary<string, string?>(
                    _environmentVariables,
                    GetEnvironmentVariableComparer()),

            EnvironmentPolicy =
                CloneEnvironmentPolicy(
                    _environmentPolicy),

            InputEncoding = _inputEncoding!,
            OutputEncoding = _outputEncoding!,
            DefaultEncoding = _defaultEncoding!,
            EnvironmentService = _environmentService
        };
    }

    /// <summary>
    /// Validates command-line arguments before storing them.
    /// </summary>
    /// <param name="arguments">The arguments to validate.</param>
    /// <param name="parameterName">The source parameter name.</param>
    private static void ValidateArguments(
        IEnumerable<string> arguments,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (arguments.Any(
                argument => argument is null))
        {
            throw new ArgumentException(
                "Command-line arguments cannot contain null values.",
                parameterName);
        }
    }

    /// <summary>
    /// Validates explicit child-process environment variables.
    /// </summary>
    /// <param name="environmentVariables">
    /// The environment variables to validate.
    /// </param>
    /// <param name="parameterName">
    /// The source parameter name.
    /// </param>
    private static void ValidateEnvironmentVariables(
        IReadOnlyDictionary<string, string?> environmentVariables,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(
            environmentVariables);

        foreach (string name in environmentVariables.Keys)
        {
            ValidateEnvironmentVariableName(
                name,
                parameterName);
        }
    }

    /// <summary>
    /// Validates a child-process environment variable name.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="parameterName">The source parameter name.</param>
    private static void ValidateEnvironmentVariableName(
        string name,
        string parameterName)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Environment variable names cannot be null, empty, or whitespace.",
                parameterName);
        }

        if (name.Contains('='))
        {
            throw new ArgumentException(
                "Environment variable names cannot contain '='.",
                parameterName);
        }

        if (name.Contains('\0'))
        {
            throw new ArgumentException(
                "Environment variable names cannot contain null characters.",
                parameterName);
        }
    }

    /// <summary>
    /// Validates a set of environment variable names.
    /// </summary>
    /// <param name="names">The names to validate.</param>
    /// <param name="parameterName">The source parameter name.</param>
    /// <param name="requireAtLeastOne">
    /// Indicates whether at least one name is required.
    /// </param>
    private static void ValidateEnvironmentVariableNames(
        IReadOnlySet<string> names,
        string parameterName,
        bool requireAtLeastOne)
    {
        ArgumentNullException.ThrowIfNull(names);

        if (requireAtLeastOne &&
            names.Count == 0)
        {
            throw new ArgumentException(
                "Environment variable collection cannot be empty.",
                parameterName);
        }

        foreach (string name in names)
        {
            ValidateEnvironmentVariableName(
                name,
                parameterName);
        }
    }

    /// <summary>
    /// Validates the consistency of a child environment policy.
    /// </summary>
    /// <param name="policy">
    /// The policy to validate.
    /// </param>
    private static void ValidateEnvironmentPolicy(
        ChildEnvironmentPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        switch (policy.Mode)
        {
            case ChildEnvironmentMode.InheritAll:
            case ChildEnvironmentMode.Isolated:
                break;

            case ChildEnvironmentMode.AllowInheritedList:
                ValidateEnvironmentVariableNames(
                    policy.AllowedInheritedVariables,
                    nameof(policy.AllowedInheritedVariables),
                    requireAtLeastOne: true);
                break;

            case ChildEnvironmentMode.AllowList:
                ValidateEnvironmentVariableNames(
                    policy.AllowedVariables,
                    nameof(policy.AllowedVariables),
                    requireAtLeastOne: true);
                break;

            case ChildEnvironmentMode.DenyList:
                ValidateEnvironmentVariableNames(
                    policy.DeniedVariables,
                    nameof(policy.DeniedVariables),
                    requireAtLeastOne: true);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(policy.Mode),
                    policy.Mode,
                    "Unsupported child environment mode.");
        }

        ValidateEnvironmentVariables(
            policy.Overrides,
            nameof(policy.Overrides));
    }

    /// <summary>
    /// Creates a defensive copy of a child environment policy.
    /// </summary>
    /// <param name="policy">
    /// The policy to copy.
    /// </param>
    /// <returns>A defensive copy of the policy.</returns>
    private ChildEnvironmentPolicy CloneEnvironmentPolicy(
        ChildEnvironmentPolicy policy)
    {
        return new ChildEnvironmentPolicy
        {
            Mode = policy.Mode,

            AllowedInheritedVariables =
                CopyEnvironmentVariableSet(
                    policy.AllowedInheritedVariables),

            AllowedVariables =
                CopyEnvironmentVariableSet(
                    policy.AllowedVariables),

            DeniedVariables =
                CopyEnvironmentVariableSet(
                    policy.DeniedVariables),

            Overrides =
                new Dictionary<string, string?>(
                    policy.Overrides,
                    GetEnvironmentVariableComparer())
        };
    }

    /// <summary>
    /// Creates a defensive copy of environment variable names.
    /// </summary>
    /// <param name="source">
    /// The environment variable names to copy.
    /// </param>
    /// <returns>A defensive copy of the names.</returns>
    private IReadOnlySet<string> CopyEnvironmentVariableSet(
        IEnumerable<string> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new HashSet<string>(
            source,
            GetEnvironmentVariableComparer());
    }

    /// <summary>
    /// Gets the platform-appropriate comparer for environment variable names.
    /// </summary>
    /// <returns>
    /// A comparer matching Windows case-insensitive and Unix-like
    /// case-sensitive environment variable semantics.
    /// </returns>
    private StringComparer
        GetEnvironmentVariableComparer()
    {
        ArgumentNullException.ThrowIfNull(_environmentService, nameof(_environmentService));
        return _environmentService.IsWindows() // 是否為Windows OS
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    }
}