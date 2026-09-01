using System.IO.Abstractions;
using System.Runtime.ExceptionServices;
using System.Text;
using CliUtilityServices.Pipes;
using CliWrap;
using FluentAssertions;
using Moq;
using Xunit;

namespace CliUtilityServices.Tests.Pipes;

public sealed class FileStreamPipeStrategyExecutionCleanupTests
{
    [Fact]
    public void ConfigurePipes_WhenSecondOutputStreamCreationFails_ShouldRollbackCreatedResources()
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

        var deletedPaths =
            new List<string>();

        SetupFileSystem(
            fileSystem,
            path,
            file,
            deletedPaths);

        var outputStreamFactory =
            new SequentialOutputStreamFactory(
                () => stdoutStream,
                () => throw new IOException(
                    expectedMessage));

        var sut =
            new FileStreamPipeStrategy(
                fileSystem.Object,
                outputStreamFactory);

        Command command =
            Cli.Wrap(
                "fake-tool");

        Action act =
            () => sut.ConfigurePipes(
                command,
                Encoding.UTF8);

        act.Should()
            .Throw<IOException>()
            .WithMessage(
                $"*{expectedMessage}*");

        stdoutStream.CanWrite.Should()
            .BeFalse();

        deletedPaths.Should()
            .HaveCount(2)
            .And.Contain(
                pathValue =>
                    pathValue.Contains(
                        "cli_stdout_",
                        StringComparison.Ordinal))
            .And.Contain(
                pathValue =>
                    pathValue.Contains(
                        "cli_stderr_",
                        StringComparison.Ordinal));

        outputStreamFactory.CreateCount.Should()
            .Be(2);

        file.Verify(
            item =>
                item.Delete(
                    It.IsAny<string>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task CleanupAsync_WhenCalledTwice_ShouldBeIdempotent()
    {
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
            file,
            deletedPaths);

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
                async () =>
                {
                    await sut.CleanupAsync();
                    await sut.CleanupAsync();
                });

        Action act =
            () =>
            {
                if (exception is not null)
                {
                    ExceptionDispatchInfo
                        .Capture(exception)
                        .Throw();
                }
            };

        act.Should()
            .NotThrow();

        deletedPaths.Should()
            .HaveCount(2);

        outputStreamFactory.CreateCount.Should()
            .Be(2);

        file.Verify(
            item =>
                item.Delete(
                    It.IsAny<string>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task CleanupAsync_WhenStdoutFlushFails_ShouldStillDisposeBothStreamsAndDeleteBothFiles()
    {
        const string expectedMessage =
            "stdout flush failed";

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
            file,
            deletedPaths);

        stdoutStream
            .SetupGet(
                stream =>
                    stream.CanWrite)
            .Returns(true);

        stdoutStream
            .Setup(
                stream =>
                    stream.FlushAsync(
                        It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new IOException(
                    expectedMessage));

        stdoutStream
            .Setup(
                stream =>
                    stream.DisposeAsync())
            .Returns(
                ValueTask.CompletedTask);

        stderrStream
            .SetupGet(
                stream =>
                    stream.CanWrite)
            .Returns(true);

        stderrStream
            .Setup(
                stream =>
                    stream.FlushAsync(
                        It.IsAny<CancellationToken>()))
            .Returns(
                Task.CompletedTask);

        stderrStream
            .Setup(
                stream =>
                    stream.DisposeAsync())
            .Returns(
                ValueTask.CompletedTask);

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
            () =>
            {
                if (exception is not null)
                {
                    ExceptionDispatchInfo
                        .Capture(exception)
                        .Throw();
                }
            };

        act.Should()
            .Throw<IOException>()
            .WithMessage(
                $"*{expectedMessage}*");

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

        deletedPaths.Should()
            .HaveCount(2);

        outputStreamFactory.CreateCount.Should()
            .Be(2);

        file.Verify(
            item =>
                item.Delete(
                    It.IsAny<string>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ConfigurePipes_WhenCalledMoreThanOnce_ShouldRejectReuse()
    {
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
            file,
            deletedPaths);

        var outputStreamFactory =
            new SequentialOutputStreamFactory(
                () => new MemoryStream(),
                () => new MemoryStream());

        var sut =
            new FileStreamPipeStrategy(
                fileSystem.Object,
                outputStreamFactory);

        Command command =
            Cli.Wrap(
                "fake-tool");

        sut.ConfigurePipes(
            command,
            Encoding.UTF8);

        Action act =
            () => sut.ConfigurePipes(
                command,
                Encoding.UTF8);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*already been configured*");

        await sut.CleanupAsync();

        outputStreamFactory.CreateCount.Should()
            .Be(2);
    }

    /// <summary>
    /// Configures strict file-system mocks required by the file-backed pipe
    /// strategy without relying on recursive default mocks.
    /// </summary>
    /// <param name="fileSystem">
    /// The file-system mock.
    /// </param>
    /// <param name="path">
    /// The path-service mock.
    /// </param>
    /// <param name="file">
    /// The file-service mock.
    /// </param>
    /// <param name="deletedPaths">
    /// The collection that records deleted temporary paths.
    /// </param>
    private static void SetupFileSystem(
        Mock<IFileSystem> fileSystem,
        Mock<IPath> path,
        Mock<IFile> file,
        ICollection<string> deletedPaths)
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

        file
            .Setup(
                item =>
                    item.Delete(
                        It.IsAny<string>()))
            .Callback(
                (string filePath) =>
                    deletedPaths.Add(
                        filePath));
    }

    /// <summary>
    /// Captures an exception produced by an asynchronous operation without
    /// using blocking task operations in an xUnit test.
    /// </summary>
    /// <param name="operation">
    /// The asynchronous operation to execute.
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
            await operation();
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }
}
