using System.IO.Abstractions.TestingHelpers;
using System.Text;
using CliUtilityServices.Pipes;
using CliWrap;
using FluentAssertions;

namespace CliUtilityServices.Tests;

public sealed partial class FileStreamPipeStrategyTests
{
    [Fact]
    public void Constructor_WhenFileSystemIsNull_ShouldThrowArgumentNullException()
    {
        Action act =
            () => new FileStreamPipeStrategy(
                null!);

        act.Should()
            .Throw<ArgumentNullException>()
            .WithMessage("*fileSystem*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenStandardOutputLimitIsInvalid_ShouldThrowArgumentOutOfRangeException(
        long maximumBytes)
    {
        var fileSystem =
            new MockFileSystem();

        Action act =
            () => new FileStreamPipeStrategy(
                fileSystem,
                maximumBytes,
                1024);

        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithMessage("*greater than zero*")
            .Which.ParamName.Should()
            .Be("maxStandardOutputBytes");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenStandardErrorLimitIsInvalid_ShouldThrowArgumentOutOfRangeException(
        long maximumBytes)
    {
        var fileSystem =
            new MockFileSystem();

        Action act =
            () => new FileStreamPipeStrategy(
                fileSystem,
                1024,
                maximumBytes);

        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithMessage("*greater than zero*")
            .Which.ParamName.Should()
            .Be("maxStandardErrorBytes");
    }

    [Fact]
    public async Task ConfigurePipes_WhenCommandIsNull_ShouldThrowArgumentNullException()
    {
        var fileSystem =
            new MockFileSystem();

        await using var sut =
            new FileStreamPipeStrategy(
                fileSystem);

        Action act =
            () => sut.ConfigurePipes(
                null!,
                Encoding.UTF8);

        act.Should()
            .Throw<ArgumentNullException>()
            .WithMessage("*command*");
    }

    [Fact]
    public async Task ConfigurePipes_WhenEncodingIsNull_ShouldThrowArgumentNullException()
    {
        var fileSystem =
            new MockFileSystem();

        await using var sut =
            new FileStreamPipeStrategy(
                fileSystem);

        Command command =
            Cli.Wrap("dummy");

        Action act =
            () => sut.ConfigurePipes(
                command,
                null!);

        act.Should()
            .Throw<ArgumentNullException>()
            .WithMessage("*encoding*");
    }

    [Fact]
    public async Task GetResultAsync_WhenStrategyHasNotBeenConfigured_ShouldThrowInvalidOperationException()
    {
        var fileSystem =
            new MockFileSystem();

        await using var sut =
            new FileStreamPipeStrategy(
                fileSystem);

        Func<Task> act =
            () => sut.GetResultAsync();

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage(
                "*has not been configured*");
    }

    [Fact]
    public async Task ConfigurePipes_WhenCalledTwice_ShouldThrowInvalidOperationException()
    {
        var fileSystem =
            new MockFileSystem();

        await using var sut =
            new FileStreamPipeStrategy(
                fileSystem);

        Command command =
            Cli.Wrap("dummy");

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
    }

    [Fact]
    public async Task GetResultAsync_WhenConfigured_ShouldUseConfiguredEncoding()
    {
        Encoding.RegisterProvider(
            CodePagesEncodingProvider.Instance);

        var fileSystem =
            new MockFileSystem();

        await using var sut =
            new FileStreamPipeStrategy(
                fileSystem);

        Encoding encoding =
            Encoding.GetEncoding(950);

        Command command =
            Cli.Wrap("dummy");

        sut.ConfigurePipes(
            command,
            encoding);

        var result =
            await sut.GetResultAsync();

        result.StandardOutput.Should()
            .BeEmpty();

        result.StandardError.Should()
            .BeEmpty();
    }

    [Fact]
    public async Task DisposeAsync_WhenCalledTwice_ShouldBeIdempotent()
    {
        var fileSystem =
            new MockFileSystem();

        var sut =
            new FileStreamPipeStrategy(
                fileSystem);

        Func<Task> act =
            async () =>
            {
                await sut.DisposeAsync();
                await sut.DisposeAsync();
            };

        await act.Should()
            .NotThrowAsync();
    }

    [Fact]
    public async Task GetResultAsync_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        var fileSystem =
            new MockFileSystem();

        var sut =
            new FileStreamPipeStrategy(
                fileSystem);

        await sut.DisposeAsync();

        Func<Task> act =
            () => sut.GetResultAsync();

        await act.Should()
            .ThrowAsync<ObjectDisposedException>()
            .WithMessage(
                "*FileStreamPipeStrategy*");
    }

    [Fact]
    public async Task ConfigurePipes_WhenDisposed_ShouldThrowObjectDisposedException()
    {
        var fileSystem =
            new MockFileSystem();

        var sut =
            new FileStreamPipeStrategy(
                fileSystem);

        await sut.DisposeAsync();

        Action act =
            () => sut.ConfigurePipes(
                Cli.Wrap("dummy"),
                Encoding.UTF8);

        act.Should()
            .Throw<ObjectDisposedException>()
            .WithMessage(
                "*FileStreamPipeStrategy*");
    }
}