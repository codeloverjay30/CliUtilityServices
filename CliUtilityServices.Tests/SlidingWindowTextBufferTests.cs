using CliUtilityServices.Pipes;
using FluentAssertions;

namespace CliUtilityServices.Tests;

public sealed class SlidingWindowTextBufferTests
{
    private const string TruncationMarker =
        "[... Outputs truncated for memory defense ...]";

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenMaxLinesIsNotPositive_ShouldThrow(int maxLines)
    {
        Action act = () => new SlidingWindowTextBuffer(maxLines, 100);

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
            new SlidingWindowTextBuffer(10, maxRetainedCharacters);

        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Maximum retained characters must be greater than zero.*");
    }

    [Fact]
    public void AddLine_WhenWindowIsExactlyFull_ShouldNotMarkOutputAsTruncated()
    {
        var sut = new SlidingWindowTextBuffer(2, 100);

        sut.AddLine("first");
        sut.AddLine("second");

        string result = sut.GetSnapshot();

        result.Should().Be(
            $"first{Environment.NewLine}" +
            $"second{Environment.NewLine}");
    }

    [Fact]
    public void AddLine_WhenLineCountExceedsLimit_ShouldKeepNewestLinesAndMarkTruncated()
    {
        var sut = new SlidingWindowTextBuffer(2, 100);

        sut.AddLine("first");
        sut.AddLine("second");
        sut.AddLine("third");

        string result = sut.GetSnapshot();

        result.Should().Be(
            $"{TruncationMarker}{Environment.NewLine}" +
            $"second{Environment.NewLine}" +
            $"third{Environment.NewLine}");
    }

    [Fact]
    public void AddLine_WhenCharacterBudgetIsExceeded_ShouldEvictOldestLines()
    {
        var sut = new SlidingWindowTextBuffer(10, 6);

        sut.AddLine("abc");
        sut.AddLine("de");
        sut.AddLine("fg");

        string result = sut.GetSnapshot();

        result.Should().Be(
            $"{TruncationMarker}{Environment.NewLine}" +
            $"de{Environment.NewLine}" +
            $"fg{Environment.NewLine}");
    }

    [Fact]
    public void AddLine_WhenSingleLineExceedsCharacterBudget_ShouldKeepNewestSuffix()
    {
        var sut = new SlidingWindowTextBuffer(10, 4);

        sut.AddLine("abcdef");

        string result = sut.GetSnapshot();

        result.Should().Be(
            $"{TruncationMarker}{Environment.NewLine}" +
            $"cdef{Environment.NewLine}");
    }

    [Fact]
    public void AddLine_WhenUpstreamReportsTruncation_ShouldIncludeMarker()
    {
        var sut = new SlidingWindowTextBuffer(10, 100);

        sut.AddLine("retained", wasLineTruncated: true);

        string result = sut.GetSnapshot();

        result.Should().Be(
            $"{TruncationMarker}{Environment.NewLine}" +
            $"retained{Environment.NewLine}");
    }

    [Fact]
    public void MarkTruncated_WhenNoLinesExist_ShouldReturnOnlyMarker()
    {
        var sut = new SlidingWindowTextBuffer(10, 100);

        sut.MarkTruncated();

        string result = sut.GetSnapshot();

        result.Should().Be(
            $"{TruncationMarker}{Environment.NewLine}");
    }

    [Fact]
    public void AddLine_WhenLineIsNull_ShouldThrow()
    {
        var sut = new SlidingWindowTextBuffer(10, 100);

        Action act = () => sut.AddLine(null!);

        act.Should()
            .Throw<ArgumentNullException>()
            .WithMessage("*line*");
    }

    [Fact]
    public async Task AddLine_WhenCalledConcurrently_ShouldPreserveAccountingInvariant()
    {
        const int lineCount = 200;
        var sut = new SlidingWindowTextBuffer(lineCount, 10_000);

        Task[] tasks = Enumerable
            .Range(0, lineCount)
            .Select(index => Task.Run(() => sut.AddLine($"line-{index:D3}")))
            .ToArray();

        await Task.WhenAll(tasks);

        string result = sut.GetSnapshot();

        result.Should().NotContain(TruncationMarker);
        result.Split(
                Environment.NewLine,
                StringSplitOptions.RemoveEmptyEntries)
            .Should()
            .HaveCount(lineCount)
            .And.OnlyHaveUniqueItems();
    }
}
