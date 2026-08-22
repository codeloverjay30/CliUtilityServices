using System.Text;
using CliUtilityServices.Pipes;
using CliWrap;
using EnvironmentUtilityServices;

namespace CliUtilityServices;

public class CommandLineInputBuilder
{
    // var input = new CommandLineInput
    //     {
    //         EnvironmentService = _environmentService,
    //         Command = provider.GetExecutablePath(_environmentService),
    //         Arguments = commandLineInput.Arguments,
    //         WorkingDirectory = commandLineInput.WorkingDirectory,
    //         OutputEncoding = commandLineInput.OutputEncoding,
    //         InputEncoding = commandLineInput.InputEncoding,
    //         Validation = commandLineInput.Validation
    //     };
    private Encoding _inputEncoding;
    private Encoding _outputEncoding;

    private Encoding? _defaultEncoding;

    private ICommandPipeStrategy _pipeStrategy = new SlidingWindowPipeStrategy(500);

    /// <summary>
    /// the command or executable file name to run.
    /// </summary>
    private string _command = string.Empty;

    /// <summary>
    /// Gets the collection of arguments passed to the command.
    /// </summary>
    private IEnumerable<string> _arguments = Array.Empty<string>();

    /// <summary>
    /// working directory for the process.
    /// </summary>
    private string _workingDirectory = string.Empty;

    /// <summary>
    /// the validation strategy used by CliWrap to determine success based on exit codes.
    /// </summary>
    private CommandResultValidation _validation = CommandResultValidation.ZeroExitCode;

    private IEnvironmentService? _environmentService;

    private TimeSpan? _timeout;

    /// <summary>
    /// Configures the maximum execution duration.
    /// </summary>
    /// <param name="timeout">The maximum allowed execution duration.</param>
    /// <returns>The current builder instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the timeout is zero or negative.
    /// </exception>
    public CommandLineInputBuilder WithTimeout(TimeSpan timeout)
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


    public CommandLineInputBuilder WithCommand(string command)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));
        _command = command;
        return this;
    }

    public CommandLineInputBuilder WithArguments(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments, nameof(arguments));
        _arguments = arguments;
        return this;
    }

    public CommandLineInputBuilder AddArgument(string argument)
    {
        ArgumentNullException.ThrowIfNull(argument, nameof(argument));
        _arguments = _arguments.Append(argument);
        return this;
    }

    public CommandLineInputBuilder AddArguments(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments, nameof(arguments));
        _arguments = _arguments.Concat(arguments);
        return this;
    }

    public CommandLineInputBuilder WithInputEncoding(Encoding inputEncoding)
    {
        ArgumentNullException.ThrowIfNull(inputEncoding, nameof(inputEncoding));
        _inputEncoding = inputEncoding;
        return this;
    }
    

    public CommandLineInputBuilder WithOutputEncoding(Encoding outputEncoding)
    {
        ArgumentNullException.ThrowIfNull(outputEncoding, nameof(outputEncoding));
        _outputEncoding = outputEncoding;
        return this;
    }

    public CommandLineInputBuilder WithDefaultEncoding(Encoding? defaultEncoding)
    {
        _defaultEncoding = defaultEncoding;
        return this;
    }
    public CommandLineInputBuilder WithPipeStrategy(ICommandPipeStrategy pipeStrategy)
    {
        ArgumentNullException.ThrowIfNull(pipeStrategy, nameof(pipeStrategy));
        _pipeStrategy = pipeStrategy;
        return this;
    }

    public CommandLineInputBuilder WithWorkingDirectory(string workingDirectory)
    {
        ArgumentNullException.ThrowIfNull(workingDirectory, nameof(workingDirectory));
        _workingDirectory = workingDirectory;
        return this;
    }

    public CommandLineInputBuilder WithValidation(CommandResultValidation validation)
    {
        ArgumentNullException.ThrowIfNull(validation, nameof(validation));
        _validation = validation;
        return this;
    }

    public CommandLineInputBuilder WithEnvironmentService(IEnvironmentService environmentService)
    {
        ArgumentNullException.ThrowIfNull(environmentService, nameof(environmentService));
        _environmentService = environmentService;
        return this;
    }

    static CommandLineInputBuilder()
    {
        // 註冊編碼表
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);    
    }

    /// <summary>
    /// Builds an immutable command-line input instance.
    /// </summary>
    /// <returns>The configured command-line input.</returns>
    public CommandLineInput Build()
    {
        if (_timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(_timeout),
                _timeout,
                "Timeout must be greater than zero.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(_command, nameof(_command));
        ArgumentNullException.ThrowIfNull(_arguments, nameof(_arguments));
        ArgumentNullException.ThrowIfNull(_workingDirectory, nameof(_workingDirectory));
        ArgumentNullException.ThrowIfNull(_validation, nameof(_validation));
        ArgumentNullException.ThrowIfNull(_pipeStrategy, nameof(_pipeStrategy));
        ArgumentNullException.ThrowIfNull(_environmentService, nameof(_environmentService));

        return new CommandLineInput
        {
            PipeStrategy = _pipeStrategy,
            Command = _command,
            Arguments = _arguments,
            WorkingDirectory = _workingDirectory,
            Validation = _validation,
            Timeout = _timeout,
            InputEncoding = _inputEncoding,
            OutputEncoding = _outputEncoding,
            DefaultEncoding = _defaultEncoding,
            EnvironmentService = _environmentService
        };
    }

}
