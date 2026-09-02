using System.Buffers;
using System.IO.Abstractions;
using System.Runtime.ExceptionServices;
using System.Text;
using CliWrap;

namespace CliUtilityServices.Pipes;

/// <summary>
/// Implements a file-backed command pipe strategy that streams command output
/// to temporary files while enforcing write-time output quotas.
/// </summary>
/// <remarks>
/// Each instance represents a single command execution lifecycle. Temporary
/// files and streams are released by <see cref="CleanupAsync"/> regardless of
/// whether execution succeeds, fails, or is cancelled.
/// </remarks>
public class FileStreamPipeStrategy :
    ICommandPipeStrategy,
    IExecutionScopedPipeStrategy,
    IAsyncDisposable
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
    private readonly IOutputStreamFactory _outputStreamFactory;

    private Encoding? _outputEncoding;
    private Stream? _stdoutStream;
    private Stream? _stderrStream;

    private bool _isConfigured;
    private bool _executionCleanupCompleted;
    private volatile bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="FileStreamPipeStrategy"/> class.
    /// </summary>
    /// <param name="fileSystem">
    /// The file-system abstraction used to create and manage temporary files.
    /// </param>
    /// <param name="maxStandardOutputBytes">
    /// The maximum number of bytes that standard output may write.
    /// </param>
    /// <param name="maxStandardErrorBytes">
    /// The maximum number of bytes that standard error may write.
    /// </param>
    public FileStreamPipeStrategy(
        IFileSystem fileSystem,
        long maxStandardOutputBytes = DefaultMaxOutputBytes,
        long maxStandardErrorBytes = DefaultMaxOutputBytes)
        : this(
            fileSystem,
            new FileOutputStreamFactory(
                fileSystem),
            maxStandardOutputBytes,
            maxStandardErrorBytes)
    {
    }

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="FileStreamPipeStrategy"/> class.
    /// </summary>
    /// <param name="fileSystem">
    /// The file-system abstraction used to create and manage temporary files.
    /// </param>
    /// <param name="outputStreamFactory">
    /// The factory used to create output streams.
    /// </param>
    /// <param name="maxStandardOutputBytes">
    /// The maximum number of bytes that standard output may write.
    /// </param>
    /// <param name="maxStandardErrorBytes">
    /// The maximum number of bytes that standard error may write.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="fileSystem"/> or
    /// <paramref name="outputStreamFactory"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when an output quota is less than or equal to zero.
    /// </exception>
    internal FileStreamPipeStrategy(
        IFileSystem fileSystem,
        IOutputStreamFactory outputStreamFactory,
        long maxStandardOutputBytes = DefaultMaxOutputBytes,
        long maxStandardErrorBytes = DefaultMaxOutputBytes)
    {
        ArgumentNullException.ThrowIfNull(
            fileSystem);

        ArgumentNullException.ThrowIfNull(
            outputStreamFactory);

        ValidateOutputLimit(
            maxStandardOutputBytes,
            nameof(maxStandardOutputBytes));

        ValidateOutputLimit(
            maxStandardErrorBytes,
            nameof(maxStandardErrorBytes));

        _fileSystem =
            fileSystem;

        _outputStreamFactory =
            outputStreamFactory;

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

            if (_isConfigured)
            {
                throw new InvalidOperationException(
                    "The file stream pipe strategy has already been configured.");
            }

            Stream? stdoutFileStream = null;
            Stream? stderrFileStream = null;

            try
            {
                stdoutFileStream =
                    _outputStreamFactory.Create(
                        _stdoutFilePath);

                stderrFileStream =
                    _outputStreamFactory.Create(
                        _stderrFilePath);

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

                _outputEncoding =
                    encoding;

                Command configuredCommand =
                    command
                        .WithStandardOutputPipe(
                            PipeTarget.ToStream(
                                _stdoutStream))
                        .WithStandardErrorPipe(
                            PipeTarget.ToStream(
                                _stderrStream));

                _isConfigured = true;

                return configuredCommand;
            }
            catch
            {
                /*
                 * Preserve the configuration exception. Rollback is
                 * best-effort because cleanup must never replace the primary
                 * configuration failure.
                 */
                TryDisposeStream(
                    _stdoutStream);

                TryDisposeStream(
                    _stderrStream);

                TryDisposeStream(
                    stdoutFileStream);

                TryDisposeStream(
                    stderrFileStream);

                _stdoutStream = null;
                _stderrStream = null;
                _outputEncoding = null;

                TryDeleteTemporaryFileBestEffort(
                    _stdoutFilePath);

                TryDeleteTemporaryFileBestEffort(
                    _stderrFilePath);

                throw;
            }
        }
        finally
        {
            _fileSemaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<(
        string StandardOutput,
        string StandardError)> GetResultAsync()
    {
        ObjectDisposedException.ThrowIf(
            _isDisposed,
            this);

        await _fileSemaphore
            .WaitAsync()
            .ConfigureAwait(false);

        try
        {
            ObjectDisposedException.ThrowIf(
                _isDisposed,
                this);

            if (!_isConfigured)
            {
                throw new InvalidOperationException(
                    "The file stream pipe strategy has not been configured.");
            }

            Encoding outputEncoding =
                _outputEncoding
                ?? throw new InvalidOperationException(
                    "The file stream pipe strategy has not been configured.");

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
        finally
        {
            _fileSemaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task CleanupAsync()
    {
        await _fileSemaphore
            .WaitAsync()
            .ConfigureAwait(false);

        try
        {
            if (_executionCleanupCompleted)
            {
                return;
            }

            Exception? firstException = null;

            try
            {
                await FlushAndCloseStreamsInternalAsync()
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                firstException =
                    exception;
            }
            finally
            {
                /*
                 * File deletion must still be attempted even when stream
                 * flushing or disposal fails. Preserve the first cleanup
                 * exception while continuing with all remaining cleanup.
                 */
                TryDeleteTemporaryFile(
                    _stdoutFilePath,
                    ref firstException);

                TryDeleteTemporaryFile(
                    _stderrFilePath,
                    ref firstException);

                _executionCleanupCompleted = true;
            }

            if (firstException is not null)
            {
                ExceptionDispatchInfo
                    .Capture(firstException)
                    .Throw();
            }
        }
        finally
        {
            _fileSemaphore.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
        {
            return;
        }

        Exception? cleanupException = null;

        await _fileSemaphore
            .WaitAsync()
            .ConfigureAwait(false);

        try
        {
            if (_isDisposed)
            {
                return;
            }

            if (!_executionCleanupCompleted)
            {
                try
                {
                    await CleanupInternalAsync()
                        .ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    cleanupException =
                        exception;
                }
            }

            _isDisposed = true;
        }
        finally
        {
            _fileSemaphore.Release();
        }

        GC.SuppressFinalize(this);

        if (cleanupException is not null)
        {
            ExceptionDispatchInfo
                .Capture(cleanupException)
                .Throw();
        }
    }

    /// <summary>
    /// Cleans execution-scoped resources while the strategy semaphore is held.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous cleanup operation.
    /// </returns>
    private async Task CleanupInternalAsync()
    {
        Exception? firstException = null;

        try
        {
            await FlushAndCloseStreamsInternalAsync()
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            firstException =
                exception;
        }
        finally
        {
            TryDeleteTemporaryFile(
                _stdoutFilePath,
                ref firstException);

            TryDeleteTemporaryFile(
                _stderrFilePath,
                ref firstException);

            _executionCleanupCompleted = true;
        }

        if (firstException is not null)
        {
            ExceptionDispatchInfo
                .Capture(firstException)
                .Throw();
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

    /// <summary>
    /// Flushes and closes all configured output streams.
    /// The caller must hold the strategy semaphore before invoking this method.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous close operation.
    /// </returns>
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
            catch (Exception exception)
            {
                firstException =
                    exception;
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
            catch (Exception exception)
            {
                firstException ??=
                    exception;
            }
        }

        if (firstException is not null)
        {
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
    /// <returns>
    /// A task that represents the asynchronous cleanup operation.
    /// </returns>
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
    /// Reads captured command output from a file while enforcing the configured
    /// in-memory read limit.
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

        /*
         * Open the capture file directly. A configured strategy owns these
         * files for the entire result-read lifecycle, so a missing file is a
         * lifecycle failure and must not be silently converted to empty output.
         */
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

            byte[] rentedBuffer =
                ArrayPool<byte>.Shared.Rent(
                    maxBytes);

            try
            {
                int bytesRead =
                    await ReadFullyAsync(
                            stream,
                            rentedBuffer.AsMemory(
                                0,
                                maxBytes))
                        .ConfigureAwait(false);

                return
                    "[... Target file output was too large and truncated for memory defense ...]" +
                    Environment.NewLine +
                    encoding.GetString(
                        rentedBuffer,
                        0,
                        bytesRead);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(
                    rentedBuffer,
                    clearArray: true);
            }
        }

        int bufferLength =
            checked((int)fileLength);

        byte[] rentedFullBuffer =
            ArrayPool<byte>.Shared.Rent(
                bufferLength);

        try
        {
            int fullBytesRead =
                await ReadFullyAsync(
                        stream,
                        rentedFullBuffer.AsMemory(
                            0,
                            bufferLength))
                    .ConfigureAwait(false);

            return encoding.GetString(
                rentedFullBuffer,
                0,
                fullBytesRead);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(
                rentedFullBuffer,
                clearArray: true);
        }
    }

    /// <summary>
    /// Reads from the specified stream until the destination buffer is full or
    /// the end of the stream is reached.
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

    /// <summary>
    /// Attempts to dispose a stream without replacing an existing primary
    /// configuration exception.
    /// </summary>
    /// <param name="stream">
    /// The stream to dispose, or <see langword="null"/>.
    /// </param>
    private static void TryDisposeStream(
        Stream? stream)
    {
        if (stream is null)
        {
            return;
        }

        try
        {
            stream.Dispose();
        }
        catch
        {
            /*
             * Configuration rollback is best-effort. The original
             * configuration exception must remain authoritative.
             */
        }
    }

    /// <summary>
    /// Attempts to delete a temporary output file and records the first
    /// cleanup failure without preventing subsequent cleanup operations.
    /// </summary>
    /// <param name="filePath">
    /// The temporary file path.
    /// </param>
    /// <param name="firstException">
    /// The first cleanup exception observed during the current cleanup
    /// operation.
    /// </param>
    private void TryDeleteTemporaryFile(
        string filePath,
        ref Exception? firstException)
    {
        try
        {
            /*
             * File.Delete semantics are already tolerant of a missing file.
             * Avoid an Exists/Delete check-then-act window.
             */
            _fileSystem.File.Delete(
                filePath);
        }
        catch (Exception exception)
            when (exception is IOException
                or UnauthorizedAccessException)
        {
            firstException ??=
                exception;
        }
    }

    /// <summary>
    /// Attempts to delete a temporary output file during configuration
    /// rollback without replacing the primary configuration exception.
    /// </summary>
    /// <param name="filePath">
    /// The temporary file path.
    /// </param>
    private void TryDeleteTemporaryFileBestEffort(
        string filePath)
    {
        try
        {
            _fileSystem.File.Delete(
                filePath);
        }
        catch (IOException)
        {
            /*
             * Preserve the primary configuration exception.
             */
        }
        catch (UnauthorizedAccessException)
        {
            /*
             * Preserve the primary configuration exception.
             */
        }
    }
}