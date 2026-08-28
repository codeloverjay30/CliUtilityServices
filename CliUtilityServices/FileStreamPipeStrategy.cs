// File: CliUtilityServices/Pipes/FileStreamPipeStrategy.cs
using System.IO.Abstractions;
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


    // GetResultAsync() remains unchanged.

    // FlushAndCloseStreamsInternalAsync() remains unchanged.

    // ReadFileWithLimitAsync() remains unchanged.

    // DisposeAsync() remains unchanged.

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
        }
        finally
        {
            _fileSemaphore.Release();
        }

        await FlushAndCloseStreamsInternalAsync()
            .ConfigureAwait(false);

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


    private async Task FlushAndCloseStreamsInternalAsync()
    {
        // 🎯 同樣要在進鎖前防禦，避免非同步併發下生命週期錯亂
        if (_isDisposed) return;

        await _fileSemaphore.WaitAsync();
        try
        {
            if (_stdoutStream != null)
            {
                await _stdoutStream.FlushAsync();
                await _stdoutStream.DisposeAsync();
                _stdoutStream = null;
            }

            if (_stderrStream != null)
            {
                await _stderrStream.FlushAsync();
                await _stderrStream.DisposeAsync();
                _stderrStream = null;
            }
        }
        finally
        {
            _fileSemaphore.Release();
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
        // 💡 快速切換狀態旗標，讓後續併發的呼叫在 WaitAsync() 前被精準攔截
        if (_isDisposed) return;
        _isDisposed = true;

        // 確保在銷毀鎖之前，所有寫入流與內部非同步資源被釋放
        if (_stdoutStream != null)
        {
            await _stdoutStream.FlushAsync();
            await _stdoutStream.DisposeAsync();
            _stdoutStream = null;
        }

        if (_stderrStream != null)
        {
            await _stderrStream.FlushAsync();
            await _stderrStream.DisposeAsync();
            _stderrStream = null;
        }

        // 清除實體磁碟暫存隱患
        try
        {
            if (_fileSystem.File.Exists(_stdoutFilePath)) _fileSystem.File.Delete(_stdoutFilePath);
            if (_fileSystem.File.Exists(_stderrFilePath)) _fileSystem.File.Delete(_stderrFilePath);
        }
        catch
        {
            // 防禦性空攔截，避免邊緣 I/O 崩潰阻斷 GC 處置鏈
        }

        // 🎯 確定外部不會再存取、內部資源全部關閉後，最後才 Dispose 鎖物件
        _fileSemaphore.Dispose();
        GC.SuppressFinalize(this);
    }
}