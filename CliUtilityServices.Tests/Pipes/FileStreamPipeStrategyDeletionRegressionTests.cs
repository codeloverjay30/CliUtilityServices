using System.IO.Abstractions;
using System.Runtime.ExceptionServices;
using System.Text;
using CliUtilityServices.Pipes;
using CliWrap;
using FluentAssertions;
using Moq;
using Xunit;

namespace CliUtilityServices.Tests.Pipes;

public sealed class FileStreamPipeStrategyDeletionRegressionTests
{
    [Fact]
    public async Task CleanupAsync_WhenStdoutDeletionFails_ShouldStillDeleteStderrAndSurfaceStdoutFailure()
    {
        const string expectedMessage =
            "stdout temp deletion failed";

        var fileSystem =
            new Mock<IFileSystem>(
                MockBehavior.Strict);

        var path =
            new Mock<IPath>(
                MockBehavior.Strict);

        var file =
            new Mock<IFile>(
                MockBehavior.Strict);

        var deletedPaths =
            new List<string>();

        SetupFileSystem(
            fileSystem,
            path,
            file);

        file
            .Setup(
                item =>
                    item.Delete(
                        It.IsAny<string>()))
            .Callback(
                (string filePath) =>
                {
                    deletedPaths.Add(
                        filePath);

                    if (filePath.Contains(
                            "cli_stdout_",
                            StringComparison.Ordinal))
                    {
                        throw new IOException(
                            expectedMessage);
                    }
                });

        var outputStreamFactory =
            new SequentialOutputStreamFactory(
                () => new MemoryStream(),
                () => new MemoryStream());

        var sut =
            new FileStreamPipeStrategy(
                fileSystem.Object,
                outputStreamFactory);

        sut.ConfigurePipes(
            Cli.Wrap(
                "fake-tool"),
            Encoding.UTF8);

        Exception? exception =
            await CaptureExceptionAsync(
                () => sut.CleanupAsync());

        Action act =
            () => RethrowCapturedException(
                exception);

        act.Should()
            .Throw<IOException>()
            .WithMessage(
                $"*{expectedMessage}*");

        deletedPaths.Should()
            .HaveCount(2)
            .And.Contain(
                item =>
                    item.Contains(
                        "cli_stdout_",
                        StringComparison.Ordinal))
            .And.Contain(
                item =>
                    item.Contains(
                        "cli_stderr_",
                        StringComparison.Ordinal));

        file.Verify(
            item =>
                item.Delete(
                    It.IsAny<string>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task CleanupAsync_WhenStderrDeletionFails_ShouldSurfaceUnauthorizedAccessFailure()
    {
        const string expectedMessage =
            "stderr temp deletion denied";

        var fileSystem =
            new Mock<IFileSystem>(
                MockBehavior.Strict);

        var path =
            new Mock<IPath>(
                MockBehavior.Strict);

        var file =
            new Mock<IFile>(
                MockBehavior.Strict);

        var deletedPaths =
            new List<string>();

        SetupFileSystem(
            fileSystem,
            path,
            file);

        file
            .Setup(
                item =>
                    item.Delete(
                        It.IsAny<string>()))
            .Callback(
                (string filePath) =>
                {
                    deletedPaths.Add(
                        filePath);

                    if (filePath.Contains(
                            "cli_stderr_",
                            StringComparison.Ordinal))
                    {
                        throw new UnauthorizedAccessException(
                            expectedMessage);
                    }
                });

        var outputStreamFactory =
            new SequentialOutputStreamFactory(
                () => new MemoryStream(),
                () => new MemoryStream());

        var sut =
            new FileStreamPipeStrategy(
                fileSystem.Object,
                outputStreamFactory);

        sut.ConfigurePipes(
            Cli.Wrap(
                "fake-tool"),
            Encoding.UTF8);

        Exception? exception =
            await CaptureExceptionAsync(
                () => sut.CleanupAsync());

        Action act =
            () => RethrowCapturedException(
                exception);

        act.Should()
            .Throw<UnauthorizedAccessException>()
            .WithMessage(
                $"*{expectedMessage}*");

        deletedPaths.Should()
            .HaveCount(2);

        file.Verify(
            item =>
                item.Delete(
                    It.IsAny<string>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task CleanupAsync_WhenFlushAndDeletionFail_ShouldPreserveFlushFailureAndAttemptBothDeletions()
    {
        const string expectedFlushMessage =
            "stdout flush failed";

        const string deleteMessage =
            "stdout temp deletion failed";

        var fileSystem =
            new Mock<IFileSystem>(
                MockBehavior.Strict);

        var path =
            new Mock<IPath>(
                MockBehavior.Strict);

        var file =
            new Mock<IFile>(
                MockBehavior.Strict);

        var stdoutStream =
            new Mock<Stream>(
                MockBehavior.Strict);

        var stderrStream =
            new Mock<Stream>(
                MockBehavior.Strict);

        var deletedPaths =
            new List<string>();

        SetupFileSystem(
            fileSystem,
            path,
            file);

        SetupWritableStream(
            stdoutStream,
            flushException:
                new IOException(
                    expectedFlushMessage));

        SetupWritableStream(
            stderrStream);

        file
            .Setup(
                item =>
                    item.Delete(
                        It.IsAny<string>()))
            .Callback(
                (string filePath) =>
                {
                    deletedPaths.Add(
                        filePath);

                    if (filePath.Contains(
                            "cli_stdout_",
                            StringComparison.Ordinal))
                    {
                        throw new IOException(
                            deleteMessage);
                    }
                });

        var outputStreamFactory =
            new SequentialOutputStreamFactory(
                () => stdoutStream.Object,
                () => stderrStream.Object);

        var sut =
            new FileStreamPipeStrategy(
                fileSystem.Object,
                outputStreamFactory);

        sut.ConfigurePipes(
            Cli.Wrap(
                "fake-tool"),
            Encoding.UTF8);

        Exception? exception =
            await CaptureExceptionAsync(
                () => sut.CleanupAsync());

        Action act =
            () => RethrowCapturedException(
                exception);

        act.Should()
            .Throw<IOException>()
            .WithMessage(
                $"*{expectedFlushMessage}*");

        deletedPaths.Should()
            .HaveCount(2)
            .And.Contain(
                item =>
                    item.Contains(
                        "cli_stdout_",
                        StringComparison.Ordinal))
            .And.Contain(
                item =>
                    item.Contains(
                        "cli_stderr_",
                        StringComparison.Ordinal));

        stdoutStream.Verify(
            stream =>
                stream.FlushAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);

        stdoutStream.Verify(
            stream =>
                stream.DisposeAsync(),
            Times.Once);

        stderrStream.Verify(
            stream =>
                stream.FlushAsync(
                    It.IsAny<CancellationToken>()),
            Times.Once);

        stderrStream.Verify(
            stream =>
                stream.DisposeAsync(),
            Times.Once);

        file.Verify(
            item =>
                item.Delete(
                    It.IsAny<string>()),
            Times.Exactly(2));
    }

    [Fact]
    public void ConfigurePipes_WhenStreamCreationAndRollbackDeletionFail_ShouldPreserveConfigurationFailure()
    {
        const string expectedMessage =
            "stderr stream creation failed";

        var fileSystem =
            new Mock<IFileSystem>(
                MockBehavior.Strict);

        var path =
            new Mock<IPath>(
                MockBehavior.Strict);

        var file =
            new Mock<IFile>(
                MockBehavior.Strict);

        var stdoutStream =
            new MemoryStream();

        SetupFileSystem(
            fileSystem,
            path,
            file);

        file
            .Setup(
                item =>
                    item.Delete(
                        It.IsAny<string>()))
            .Throws(
                new IOException(
                    "rollback deletion failed"));

        var outputStreamFactory =
            new SequentialOutputStreamFactory(
                () => stdoutStream,
                () => throw new IOException(
                    expectedMessage));

        var sut =
            new FileStreamPipeStrategy(
                fileSystem.Object,
                outputStreamFactory);

        Action act =
            () => sut.ConfigurePipes(
                Cli.Wrap(
                    "fake-tool"),
                Encoding.UTF8);

        act.Should()
            .Throw<IOException>()
            .WithMessage(
                $"*{expectedMessage}*");

        stdoutStream.CanWrite.Should()
            .BeFalse();

        outputStreamFactory.CreateCount.Should()
            .Be(2);

        file.Verify(
            item =>
                item.Delete(
                    It.IsAny<string>()),
            Times.Exactly(2));
    }

    /// <summary>
    /// Configures strict file-system mocks required by the file-backed pipe
    /// strategy without relying on recursive default mocks.
    /// </summary>
    /// <param name="fileSystem">
    /// The file-system abstraction mock.
    /// </param>
    /// <param name="path">
    /// The path-service mock.
    /// </param>
    /// <param name="file">
    /// The file-service mock.
    /// </param>
    private static void SetupFileSystem(
        Mock<IFileSystem> fileSystem,
        Mock<IPath> path,
        Mock<IFile> file)
    {
        const string tempPath =
            "/tmp/";

        fileSystem
            .SetupGet(
                system =>
                    system.Path)
            .Returns(
                path.Object);

        fileSystem
            .SetupGet(
                system =>
                    system.File)
            .Returns(
                file.Object);

        path
            .Setup(
                item =>
                    item.GetTempPath())
            .Returns(
                tempPath);

        path
            .Setup(
                item =>
                    item.Combine(
                        tempPath,
                        It.IsAny<string>()))
            .Returns(
                (string first, string second) =>
                    $"{first}{second}");
    }

    /// <summary>
    /// Configures a strict writable stream mock for cleanup testing.
    /// </summary>
    /// <param name="stream">
    /// The stream mock to configure.
    /// </param>
    /// <param name="flushException">
    /// The optional exception raised by asynchronous flush.
    /// </param>
    private static void SetupWritableStream(
        Mock<Stream> stream,
        Exception? flushException = null)
    {
        stream
            .SetupGet(
                item =>
                    item.CanWrite)
            .Returns(true);

        if (flushException is null)
        {
            stream
                .Setup(
                    item =>
                        item.FlushAsync(
                            It.IsAny<CancellationToken>()))
                .Returns(
                    Task.CompletedTask);
        }
        else
        {
            stream
                .Setup(
                    item =>
                        item.FlushAsync(
                            It.IsAny<CancellationToken>()))
                .ThrowsAsync(
                    flushException);
        }

        stream
            .Setup(
                item =>
                    item.DisposeAsync())
            .Returns(
                ValueTask.CompletedTask);
    }

    /// <summary>
    /// Captures an exception from an asynchronous operation without blocking
    /// an xUnit test thread.
    /// </summary>
    /// <param name="operation">
    /// The asynchronous operation to invoke.
    /// </param>
    /// <returns>
    /// The captured exception, or <see langword="null"/> when the operation
    /// completes successfully.
    /// </returns>
    private static async Task<Exception?> CaptureExceptionAsync(
        Func<Task> operation)
    {
        try
        {
            await operation()
                .ConfigureAwait(false);

            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    /// <summary>
    /// Rethrows a previously captured exception while preserving its original
    /// stack trace.
    /// </summary>
    /// <param name="exception">
    /// The captured exception to rethrow.
    /// </param>
    private static void RethrowCapturedException(
        Exception? exception)
    {
        if (exception is null)
        {
            return;
        }

        ExceptionDispatchInfo
            .Capture(exception)
            .Throw();
    }

    /// <summary>
    /// Provides deterministic output streams to the system under test without
    /// using a dynamic proxy for the internal
    /// <see cref="IOutputStreamFactory"/> contract.
    /// </summary>
    private sealed class SequentialOutputStreamFactory :
        IOutputStreamFactory
    {
        private readonly Queue<Func<Stream>>
            _streamFactories;

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="SequentialOutputStreamFactory"/> class.
        /// </summary>
        /// <param name="streamFactories">
        /// The ordered stream factories to invoke.
        /// </param>
        public SequentialOutputStreamFactory(
            params Func<Stream>[] streamFactories)
        {
            ArgumentNullException.ThrowIfNull(
                streamFactories);

            _streamFactories =
                new Queue<Func<Stream>>(
                    streamFactories);
        }

        /// <summary>
        /// Gets the number of calls made to <see cref="Create"/>.
        /// </summary>
        public int CreateCount
        {
            get;
            private set;
        }

        /// <summary>
        /// Creates the next configured output stream.
        /// </summary>
        /// <param name="path">
        /// The destination path requested by the system under test.
        /// </param>
        /// <returns>
        /// The next configured stream.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when no configured stream factory remains.
        /// </exception>
        public Stream Create(
            string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(
                path);

            CreateCount =
                checked(
                    CreateCount + 1);

            if (_streamFactories.Count == 0)
            {
                throw new InvalidOperationException(
                    "No output stream factory remains for this test.");
            }

            return _streamFactories
                .Dequeue()
                .Invoke();
        }
    }
}
