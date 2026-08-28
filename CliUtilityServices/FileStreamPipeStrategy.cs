// File: CliUtilityServices/Pipes/FileStreamPipeStrategy.cs
using System.IO.Abstractions;
using System.Runtime.ExceptionServices;
using System.Text;
using CliWrap;

namespace CliUtilityServices.Pipes;

/// <summary>
/// Implements a file-backed command pipe strategy that streams command output
/// to temporary files while enforcing write-time output quotas.
/// </summary>
public class FileStreamPipeStrategy : ICommandPipeStrategy, IAsyncDisposable
{
    private const long DefaultMaxOutputBytes =
        50L * 1024 * 1024;

    private readonly IFileSystem _fileSystem;
    private readonly string _stdoutFilePath;
    private readonly string _stderrFilePath;

    private readonly long _maxStandardOutputBytes;
    private readonly long _maxStandardErrorBytes;

    private readonly SemaphoreSlim _fileSemaphore =
        new(1, 1);

    private Encoding? _outputEncoding;

    private Stream? _stdoutStream;
    private Stream? _stderrStream;

    private volatile bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="FileStreamPipeStrategy"/> class.
    /// </summary>
    /// <param name="fileSystem">
    /// The file system abstraction used to create and manage temporary files.
    /// </param>
    /// <param name="maxStandardOutputBytes">
    /// The maximum number of bytes that standard output may write.
    /// </param>
    /// <param name="maxStandardErrorBytes">
    /// The maximum number of bytes that standard error may write.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="fileSystem"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when an output quota is less than or equal to zero.
    /// </exception>
    public FileStreamPipeStrategy(
        IFileSystem fileSystem,
        long maxStandardOutputBytes = DefaultMaxOutputBytes,
        long maxStandardErrorBytes = DefaultMaxOutputBytes)
    {
        ArgumentNullException.ThrowIfNull(
            fileSystem);

        ValidateOutputLimit(
            maxStandardOutputBytes,
            nameof(maxStandardOutputBytes));

        ValidateOutputLimit(
            maxStandardErrorBytes,
            nameof(maxStandardErrorBytes));

        _fileSystem =
            fileSystem;

        _maxStandardOutputBytes =
            maxStandardOutputBytes;

        _maxStandardErrorBytes =
            maxStandardErrorBytes;

        string tempDir =
            _fileSystem.Path.GetTempPath();

        _stdoutFilePath =
            _fileSystem.Path.Combine(
                tempDir,
                $"cli_stdout_{Guid.NewGuid():N}.tmp");

        _stderrFilePath =
            _fileSystem.Path.Combine(
                tempDir,
                $"cli_stderr_{Guid.NewGuid():N}.tmp");
    }

    /// <inheritdoc />
    public Command ConfigurePipes(
        Command command,
        Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(
            command);

        ArgumentNullException.ThrowIfNull(
            encoding);

        ObjectDisposedException.ThrowIf(
            _isDisposed,
            this);

        _fileSemaphore.Wait();

        try
        {
            ObjectDisposedException.ThrowIf(
                _isDisposed,
                this);

            if (_stdoutStream is not null ||
                _stderrStream is not null)
            {
                throw new InvalidOperationException(
                    "The file stream pipe strategy has already been configured.");
            }

            Stream? stdoutFileStream = null;
            Stream? stderrFileStream = null;

            try
            {
                stdoutFileStream =
                    _fileSystem.File.Create(
                        _stdoutFilePath,
                        4096,
                        FileOptions.Asynchronous);

                stderrFileStream =
                    _fileSystem.File.Create(
                        _stderrFilePath,
                        4096,
                        FileOptions.Asynchronous);

                _stdoutStream =
                    new BoundedWriteStream(
                        stdoutFileStream,
                        _maxStandardOutputBytes,
                        "standard output");

                stdoutFileStream = null;

                _stderrStream =
                    new BoundedWriteStream(
                        stderrFileStream,
                        _maxStandardErrorBytes,
                        "standard error");

                stderrFileStream = null;

                _outputEncoding = encoding;

                return command
                    .WithStandardOutputPipe(
                        PipeTarget.ToStream(
                            _stdoutStream))
                    .WithStandardErrorPipe(
                        PipeTarget.ToStream(
                            _stderrStream));
            }
            catch
            {
                _stdoutStream?.Dispose();
                _stderrStream?.Dispose();

                stdoutFileStream?.Dispose();
                stderrFileStream?.Dispose();

                _stdoutStream = null;
                _stderrStream = null;
                _outputEncoding = null;

                throw;
            }
        }
        finally
        {
            _fileSemaphore.Release();
        }
    }

    /// <summary>
    /// Validates that an output quota is a positive byte count.
    /// </summary>
    /// <param name="maximumBytes">
    /// The maximum permitted number of bytes.
    /// </param>
    /// <param name="parameterName">
    /// The name of the parameter being validated.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maximumBytes"/> is less than or equal to zero.
    /// </exception>
    private static void ValidateOutputLimit(
        long maximumBytes,
        string parameterName)
    {
        if (maximumBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                maximumBytes,
                "Maximum output bytes must be greater than zero.");
        }
    }


    /// <inheritdoc />
    /// <inheritdoc />
    public async Task<(string StandardOutput, string StandardError)> GetResultAsync()
    {
        ObjectDisposedException.ThrowIf(
            _isDisposed,
            this);

        Encoding outputEncoding;

        await _fileSemaphore
            .WaitAsync()
            .ConfigureAwait(false);

        try
        {
            ObjectDisposedException.ThrowIf(
                _isDisposed,
                this);

            outputEncoding =
                _outputEncoding
                ?? throw new InvalidOperationException(
                    "The file stream pipe strategy has not been configured.");

            await FlushAndCloseStreamsInternalAsync()
                .ConfigureAwait(false);
        }
        finally
        {
            _fileSemaphore.Release();
        }

        const int maxReadBytes =
            10 * 1024 * 1024;

        string stdout =
            await ReadFileWithLimitAsync(
                    _stdoutFilePath,
                    maxReadBytes,
                    outputEncoding)
                .ConfigureAwait(false);

        string stderr =
            await ReadFileWithLimitAsync(
                    _stderrFilePath,
                    maxReadBytes,
                    outputEncoding)
                .ConfigureAwait(false);

        return (
            stdout,
            stderr);
    }



    /// <summary>
    /// Flushes and closes all configured output streams.
    /// The caller must hold the strategy semaphore before invoking this method.
    /// </summary>
    private async Task FlushAndCloseStreamsInternalAsync()
    {
        Stream? stdoutStream =
            _stdoutStream;

        Stream? stderrStream =
            _stderrStream;

        _stdoutStream = null;
        _stderrStream = null;

        Exception? firstException = null;

        if (stdoutStream is not null)
        {
            try
            {
                await FlushAndDisposeStreamAsync(
                        stdoutStream)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                firstException = ex;
            }
        }

        if (stderrStream is not null)
        {
            try
            {
                await FlushAndDisposeStreamAsync(
                        stderrStream)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                firstException ??= ex;
            }
        }

        if (firstException is not null)
        {
            // 為了保留原始 exception stack trace。
            ExceptionDispatchInfo
                .Capture(firstException)
                .Throw();
        }
    }

    /// <summary>
    /// Flushes and asynchronously disposes the specified stream.
    /// </summary>
    /// <param name="stream">
    /// The stream to flush and dispose.
    /// </param>
    private static async Task FlushAndDisposeStreamAsync(
        Stream stream)
    {
        try
        {
            await stream
                .FlushAsync()
                .ConfigureAwait(false);
        }
        finally
        {
            await stream
                .DisposeAsync()
                .ConfigureAwait(false);
        }
    }



    /// <summary>
    /// Reads captured command output from a file while enforcing
    /// the configured in-memory read limit.
    /// </summary>
    /// <param name="filePath">
    /// The path of the captured output file.
    /// </param>
    /// <param name="maxBytes">
    /// The maximum number of bytes that may be loaded into memory.
    /// </param>
    /// <param name="encoding">
    /// The encoding used to decode the captured output.
    /// </param>
    /// <returns>
    /// The decoded captured output.
    /// </returns>
    private async Task<string> ReadFileWithLimitAsync(
        string filePath,
        int maxBytes,
        Encoding encoding)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(
            filePath);

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            maxBytes);

        ArgumentNullException.ThrowIfNull(
            encoding);

        if (!_fileSystem.File.Exists(
                filePath))
        {
            return string.Empty;
        }

        using Stream stream =
            _fileSystem.File.OpenRead(
                filePath);

        long fileLength =
            stream.Length;

        if (fileLength == 0)
        {
            return string.Empty;
        }

        if (fileLength > maxBytes)
        {
            stream.Seek(
                -maxBytes,
                SeekOrigin.End);

            byte[] buffer =
                new byte[maxBytes];

            int bytesRead =
                await ReadFullyAsync(
                        stream,
                        buffer)
                    .ConfigureAwait(false);

            return
                "[... Target file output was too large and truncated for memory defense ...]" +
                Environment.NewLine +
                encoding.GetString(
                    buffer,
                    0,
                    bytesRead);
        }

        int bufferLength =
            checked((int)fileLength);

        byte[] fullBuffer =
            new byte[bufferLength];

        int fullBytesRead =
            await ReadFullyAsync(
                    stream,
                    fullBuffer)
                .ConfigureAwait(false);

        return encoding.GetString(
            fullBuffer,
            0,
            fullBytesRead);
    }


    /// <summary>
    /// Reads from the specified stream until the destination buffer is full
    /// or the end of the stream is reached.
    /// </summary>
    /// <param name="stream">
    /// The source stream.
    /// </param>
    /// <param name="buffer">
    /// The destination buffer.
    /// </param>
    /// <returns>
    /// The total number of bytes read.
    /// </returns>
    private static async Task<int> ReadFullyAsync(
        Stream stream,
        Memory<byte> buffer)
    {
        ArgumentNullException.ThrowIfNull(
            stream);

        int totalBytesRead = 0;

        while (totalBytesRead < buffer.Length)
        {
            int bytesRead =
                await stream.ReadAsync(
                        buffer[totalBytesRead..])
                    .ConfigureAwait(false);

            if (bytesRead == 0)
            {
                break;
            }

            totalBytesRead =
                checked(
                    totalBytesRead +
                    bytesRead);
        }

        return totalBytesRead;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        await _fileSemaphore
            .WaitAsync()
            .ConfigureAwait(false);

        try
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            if (_stdoutStream is not null)
            {
                await _stdoutStream
                    .FlushAsync()
                    .ConfigureAwait(false);

                await _stdoutStream
                    .DisposeAsync()
                    .ConfigureAwait(false);

                _stdoutStream = null;
            }

            if (_stderrStream is not null)
            {
                await _stderrStream
                    .FlushAsync()
                    .ConfigureAwait(false);

                await _stderrStream
                    .DisposeAsync()
                    .ConfigureAwait(false);

                _stderrStream = null;
            }

            TryDeleteTemporaryFile(
                _stdoutFilePath);

            TryDeleteTemporaryFile(
                _stderrFilePath);
        }
        finally
        {
            _fileSemaphore.Release();
        }

        GC.SuppressFinalize(this);
    }


    /// <summary>
    /// Attempts to delete a temporary output file.
    /// </summary>
    /// <param name="filePath">
    /// The temporary file path.
    /// </param>
    private void TryDeleteTemporaryFile(
        string filePath)
    {
        try
        {
            if (_fileSystem.File.Exists(
                    filePath))
            {
                _fileSystem.File.Delete(
                    filePath);
            }
        }
        catch (IOException)
        {
            // Cleanup is best-effort.
        }
        catch (UnauthorizedAccessException)
        {
            // Cleanup is best-effort.
        }
    }
}