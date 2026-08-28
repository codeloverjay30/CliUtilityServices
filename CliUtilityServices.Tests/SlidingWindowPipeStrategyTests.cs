using System.Text;
using CliUtilityServices.Pipes;
using CliWrap;
using FluentAssertions;

namespace CliUtilityServices.Tests;

public sealed class SlidingWindowPipeStrategyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenMaxLinesIsNotPositive_ShouldThrow(int maxLines)
    {
        Action act = () =>
            new SlidingWindowPipeStrategy(maxLines, 100);

        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Maximum retained lines must be greater than zero.*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenMaxRetainedCharactersIsNotPositive_ShouldThrow(
        int maxRetainedCharacters)
    {
        Action act = () =>
            new SlidingWindowPipeStrategy(10, maxRetainedCharacters);

        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Maximum retained characters must be greater than zero.*");
    }

    [Fact]
    public void ConfigurePipes_WhenCommandIsNull_ShouldThrow()
    {
        var sut = new SlidingWindowPipeStrategy();

        Action act = () =>
            sut.ConfigurePipes(null!, Encoding.UTF8);

        act.Should()
            .Throw<ArgumentNullException>()
            .WithMessage("*command*");
    }

    [Fact]
    public void ConfigurePipes_WhenEncodingIsNull_ShouldThrow()
    {
        var sut = new SlidingWindowPipeStrategy();
        Command command = Cli.Wrap("unused");

        Action act = () =>
            sut.ConfigurePipes(command, null!);

        act.Should()
            .Throw<ArgumentNullException>()
            .WithMessage("*encoding*");
    }

    [Fact]
    public void ConfigurePipes_WhenCalledTwice_ShouldThrow()
    {
        var sut = new SlidingWindowPipeStrategy();
        Command command = Cli.Wrap("unused");

        _ = sut.ConfigurePipes(command, Encoding.UTF8);

        Action act = () =>
            sut.ConfigurePipes(command, Encoding.UTF8);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*already been configured*");
    }

    [Fact]
    public async Task GetResultAsync_WhenStrategyHasNotBeenConfigured_ShouldThrow()
    {
        var sut = new SlidingWindowPipeStrategy();

        Func<Task> act = async () =>
            await sut.GetResultAsync();

        await act.Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*has not been configured*");
    }

    [Fact]
    public async Task GetResultAsync_WhenConfiguredWithoutOutput_ShouldReturnEmptyResult()
    {
        var sut = new SlidingWindowPipeStrategy();
        Command command = Cli.Wrap("unused");

        _ = sut.ConfigurePipes(command, Encoding.UTF8);

        (string standardOutput, string standardError) =
            await sut.GetResultAsync();

        standardOutput.Should().BeEmpty();
        standardError.Should().BeEmpty();
    }

    [Fact]
    public async Task GetResultAsync_WhenCalledTwice_ShouldReturnSameSnapshot()
    {
        var sut = new SlidingWindowPipeStrategy();
        Command command = Cli.Wrap("unused");

        _ = sut.ConfigurePipes(command, Encoding.UTF8);

        var first = await sut.GetResultAsync();
        var second = await sut.GetResultAsync();

        second.Should().Be(first);
    }
}
