// File: CliUtilityServices/CommandLineInput.cs
using System.Text;
using CliUtilityServices.Pipes;
using CliWrap;
using EnvironmentUtilityServices;

namespace CliUtilityServices;

/// <summary>
/// Represents the configuration and parameters required to execute a command-line process.
/// </summary>
public record class CommandLineInput
{
    private readonly Encoding? _inputEncoding;
    private readonly Encoding? _outputEncoding;

    private Encoding? _defaultEncoding;

    public ICommandPipeStrategy PipeStrategy { get; init; } = new SlidingWindowPipeStrategy(500);

    /// <summary>
    /// Gets the command or executable file name to run.
    /// </summary>
    public required string Command { get; init; }

    /// <summary>
    /// Gets the collection of arguments passed to the command.
    /// </summary>
    public IEnumerable<string> Arguments { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the working directory for the process.
    /// </summary>
    public string WorkingDirectory { get; init; } = string.Empty;

    /// <summary>
    /// Gets the validation strategy used by CliWrap to determine success based on exit codes.
    /// </summary>
    public CommandResultValidation Validation { get; init; } = CommandResultValidation.ZeroExitCode;

    /// <summary>
    /// Gets the encoding used for standard input. 
    /// Defaults to Windows-950 (Big5) on Windows, and UTF-8 on other platforms.
    /// </summary>
    public Encoding InputEncoding
    {
        get => _inputEncoding ?? DefaultEncoding;
        init => _inputEncoding = value;
    }

    /// <summary>
    /// Gets the encoding used to decode standard output and standard error.
    /// Defaults to Windows-950 (Big5) on Windows, and UTF-8 on other platforms.
    /// </summary>
    public Encoding OutputEncoding
    {
        get => _outputEncoding ?? DefaultEncoding;
        init => _outputEncoding = value;
    }

    /// <summary>
    /// Default encoding
    /// </summary>
    /// <remarks>
    /// For `init`,
    /// it might recieve null value, 
    /// but it defaults to <see cref="FallbackEncoding"/> (using null safety assignment)
    /// </remarks>
    public Encoding DefaultEncoding
    {
        get => _defaultEncoding ?? FallbackEncoding;
        init
        {
            value ??= FallbackEncoding;
            _defaultEncoding = value;
        }
    }

    public required IEnvironmentService EnvironmentService { get; init; }

    public Encoding FallbackEncoding
    {
        get
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                return EnvironmentService.IsWindows() ? Encoding.GetEncoding("Big5") : Encoding.UTF8;
            }
            catch
            {
                return Encoding.UTF8;
            }
        }
    }

}