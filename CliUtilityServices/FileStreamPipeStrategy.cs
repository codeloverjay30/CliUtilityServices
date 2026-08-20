// File: CliUtilityServices/Pipes/FileStreamPipeStrategy.cs
using System.IO.Abstractions;
using System.Text;
using CliWrap;

namespace CliUtilityServices.Pipes;

/// <summary>
/// Implements a file-stream-based pipe strategy to completely prevent OOM by streaming CLI outputs directly to disk.
/// Uses <see cref="SemaphoreSlim"/> to support asynchronous execution safety without deadlock.
/// </summary>
public class FileStreamPipeStrategy : ICommandPipeStrategy, IAsyncDisposable
{
    private readonly IFileSystem _fileSystem;
    private readonly string _stdoutFilePath;
    private readonly string _stderrFilePath;
    
    private readonly SemaphoreSlim _fileSemaphore = new(1, 1); 
    
    private Stream? _stdoutStream;
    private Stream? _stderrStream;
    
    // 🎯 宣告為 volatile 確保跨執行緒的可見性（Memory Barrier 防禦）
    private volatile bool _isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileStreamPipeStrategy"/> class.
    /// </summary>
    /// <param name="fileSystem">The file system abstraction.</param>
    public FileStreamPipeStrategy(IFileSystem fileSystem)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        _fileSystem = fileSystem;

        string tempDir = _fileSystem.Path.GetTempPath();
        _stdoutFilePath = _fileSystem.Path.Combine(tempDir, $"cli_stdout_{Guid.NewGuid():N}.tmp");
        _stderrFilePath = _fileSystem.Path.Combine(tempDir, $"cli_stderr_{Guid.NewGuid():N}.tmp");
    }

    /// <inheritdoc />
    public Command ConfigurePipes(Command command, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(encoding);

        // 🎯 防禦第一關：如果已被處置，在碰鎖之前直接拋出我們自訂的類別異常，避免觸碰已銷毀的 SemaphoreSlim
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        _fileSemaphore.Wait();
        try
        {
            // 雙重檢查鎖防禦 (Double-Check Guard)
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            _stdoutStream = _fileSystem.File.Create(_stdoutFilePath, 4096, FileOptions.Asynchronous);
            _stderrStream = _fileSystem.File.Create(_stderrFilePath, 4096, FileOptions.Asynchronous);

            return command
                .WithStandardOutputPipe(PipeTarget.ToStream(_stdoutStream))
                .WithStandardErrorPipe(PipeTarget.ToStream(_stderrStream));
        }
        finally
        {
            _fileSemaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<(string StandardOutput, string StandardError)> GetResultAsync()
    {
        // 🎯 防禦第一關：在碰鎖之前直接攔截，這樣就不會引發 SemaphoreSlim 的底層 ObjectDisposedException
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        await _fileSemaphore.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
        }
        finally
        {
            _fileSemaphore.Release();
        }

        // 先行關閉寫入流
        await FlushAndCloseStreamsInternalAsync();

        const int maxReadBytes = 10 * 1024 * 1024; // 10MB Limit Guard
        
        string stdout = await ReadFileWithLimitAsync(_stdoutFilePath, maxReadBytes);
        string stderr = await ReadFileWithLimitAsync(_stderrFilePath, maxReadBytes);

        return (stdout, stderr);
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

    private async Task<string> ReadFileWithLimitAsync(string filePath, int maxBytes)
    {
        if (!_fileSystem.File.Exists(filePath))
        {
            return string.Empty;
        }

        using var stream = _fileSystem.File.OpenRead(filePath);
        long fileLength = stream.Length;

        if (fileLength == 0)
        {
            return string.Empty;
        }

        if (fileLength > maxBytes)
        {
            stream.Seek(-maxBytes, SeekOrigin.End);
            byte[] buffer = new byte[maxBytes];
            _ = await stream.ReadAsync(buffer.AsMemory(0, maxBytes));
            return "[... Target file output was too large and truncated for memory defense ...]\n" + Encoding.UTF8.GetString(buffer);
        }
        else
        {
            byte[] buffer = new byte[fileLength];
            _ = await stream.ReadAsync(buffer.AsMemory(0, (int)fileLength));
            return Encoding.UTF8.GetString(buffer);
        }
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