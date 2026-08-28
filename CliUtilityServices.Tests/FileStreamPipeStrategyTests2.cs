using System.IO.Abstractions.TestingHelpers;
using System.Text;
using CliUtilityServices.Pipes;
using CliWrap;
using FluentAssertions;
using Moq;

namespace CliUtilityServices.Tests;

public sealed partial class FileStreamPipeStrategyTests
{
    [Fact]
    public async Task GetResultAsync_WhenStandardOutputFlushFails_ShouldStillDisposeBothStreams()
    {
        var fileSystem =
            new MockFileSystem();

        var stdoutStream =
            new ControllableStream(
                new IOException(
                    "Standard output flush failed."));

        var stderrStream =
            new ControllableStream();

        var streamFactory =
            new SequenceOutputStreamFactory(
                stdoutStream,
                stderrStream);

        await using var sut =
            new FileStreamPipeStrategy(
                fileSystem,
                streamFactory);

        sut.ConfigurePipes(
            Cli.Wrap("dummy"),
            Encoding.UTF8);

        Func<Task> act =
            () => sut.GetResultAsync();

        await act.Should()
            .ThrowAsync<IOException>()
            .WithMessage(
                "*Standard output flush failed*");

        stdoutStream.FlushWasCalled.Should()
            .BeTrue();

        stdoutStream.WasDisposed.Should()
            .BeTrue();

        stderrStream.FlushWasCalled.Should()
            .BeTrue();

        stderrStream.WasDisposed.Should()
            .BeTrue();
    }
}
