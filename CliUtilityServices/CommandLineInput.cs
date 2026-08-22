using System.Text;
using CliUtilityServices.Pipes;
using CliWrap;
using EnvironmentUtilityServices;

namespace CliUtilityServices;

/// <summary>
/// Represents the configuration required to execute a command-line process.
/// </summary>
public record class CommandLineInput
{
    private readonly Encoding? _inputEncoding;
    private readonly Encoding? _outputEncoding;
    private Encoding? _defaultEncoding;

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
    /// Gets the arguments passed to the executable.
    /// </summary>
    public IEnumerable<string> Arguments { get; init; }
        = Array.Empty<string>();

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
    /// Gets the environment abstraction used to resolve platform-specific behavior.
    /// </summary>
    public required IEnvironmentService EnvironmentService { get; init; }

    /// <summary>
    /// Gets a platform-appropriate fallback encoding.
    /// </summary>
    public Encoding FallbackEncoding
    {
        get
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                return EnvironmentService.IsWindows()
                    ? Encoding.GetEncoding("Big5")
                    : Encoding.UTF8;
            }
            catch
            {
                return Encoding.UTF8;
            }
        }
    }
}