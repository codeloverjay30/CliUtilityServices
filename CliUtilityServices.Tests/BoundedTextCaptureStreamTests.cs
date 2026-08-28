using System.Text;
using CliUtilityServices.Pipes;
using FluentAssertions;

namespace CliUtilityServices.Tests;

public sealed class BoundedTextCaptureStreamTests
{
    private const string TruncationMarker =
        "[... Outputs truncated for memory defense ...]";

    [Fact]
    public void Constructor_WhenEncodingIsNull_ShouldThrow()
    {
        var destination = new SlidingWindowTextBuffer(10, 100);

        Action act = () =>
            new BoundedTextCaptureStream(null!, destination, 100);

        act.Should()
            .Throw<ArgumentNullException>()
            .WithMessage("*encoding*");
    }

    [Fact]
    public void Constructor_WhenDestinationIsNull_ShouldThrow()
    {
        Action act = () =>
            new BoundedTextCaptureStream(Encoding.UTF8, null!, 100);

        act.Should()
            .Throw<ArgumentNullException>()
            .WithMessage("*destination*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenCurrentLineLimitIsNotPositive_ShouldThrow(int limit)
    {
        var destination = new SlidingWindowTextBuffer(10, 100);

        Action act = () =>
            new BoundedTextCaptureStream(Encoding.UTF8, destination, limit);

        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Maximum current line characters must be greater than zero.*");
    }

    [Fact]
    public void Write_WhenUtf8CharacterIsSplitAcrossWrites_ShouldDecodeCorrectly()
    {
        var destination = new SlidingWindowTextBuffer(10, 100);
        using var sut =
            new BoundedTextCaptureStream(Encoding.UTF8, destination, 100);

        byte[] bytes = Encoding.UTF8.GetBytes("A中B");

        sut.Write(bytes.AsSpan(0, 2));
        sut.Write(bytes.AsSpan(2));
        sut.Complete();

        destination.GetSnapshot()
            .Should()
            .Be($"A中B{Environment.NewLine}");
    }

    [Fact]
    public void Write_WhenLfTerminatesLines_ShouldCommitEachLine()
    {
        var destination = new SlidingWindowTextBuffer(10, 100);
        using var sut =
            new BoundedTextCaptureStream(Encoding.UTF8, destination, 100);

        sut.Write(Encoding.UTF8.GetBytes("first\nsecond\n"));
        sut.Complete();

        destination.GetSnapshot()
            .Should()
            .Be(
                $"first{Environment.NewLine}" +
                $"second{Environment.NewLine}");
    }

    [Fact]
    public void Write_WhenCrLfTerminatesLine_ShouldRemoveCarriageReturn()
    {
        var destination = new SlidingWindowTextBuffer(10, 100);
        using var sut =
            new BoundedTextCaptureStream(Encoding.UTF8, destination, 100);

        sut.Write(Encoding.UTF8.GetBytes("first\r\nsecond\r\n"));
        sut.Complete();

        destination.GetSnapshot()
            .Should()
            .Be(
                $"first{Environment.NewLine}" +
                $"second{Environment.NewLine}");
    }

    [Fact]
    public void Write_WhenCrLfIsSplitAcrossWrites_ShouldStillNormalizeLineEnding()
    {
        var destination = new SlidingWindowTextBuffer(10, 100);
        using var sut =
            new BoundedTextCaptureStream(Encoding.UTF8, destination, 100);

        sut.Write(Encoding.UTF8.GetBytes("first\r"));
        sut.Write(Encoding.UTF8.GetBytes("\nsecond"));
        sut.Complete();

        destination.GetSnapshot()
            .Should()
            .Be(
                $"first{Environment.NewLine}" +
                $"second{Environment.NewLine}");
    }

    [Fact]
    public void Write_WhenCrLfFollowsLineAtExactCapacity_ShouldNotLosePayloadOrMarkTruncated()
    {
        var destination = new SlidingWindowTextBuffer(10, 100);
        using var sut =
            new BoundedTextCaptureStream(Encoding.UTF8, destination, 3);

        sut.Write(Encoding.UTF8.GetBytes("abc\r\n"));
        sut.Complete();

        destination.GetSnapshot()
            .Should()
            .Be($"abc{Environment.NewLine}");
    }

    [Fact]
    public void Write_WhenUnterminatedLineExceedsLimit_ShouldKeepNewestSuffixAndMarkTruncated()
    {
        var destination = new SlidingWindowTextBuffer(10, 100);
        using var sut =
            new BoundedTextCaptureStream(Encoding.UTF8, destination, 4);

        sut.Write(Encoding.UTF8.GetBytes("abcdefgh"));
        sut.Complete();

        destination.GetSnapshot()
            .Should()
            .Be(
                $"{TruncationMarker}{Environment.NewLine}" +
                $"efgh{Environment.NewLine}");
    }

    [Fact]
    public void Complete_WhenFinalLineIsUnterminated_ShouldCommitIt()
    {
        var destination = new SlidingWindowTextBuffer(10, 100);
        using var sut =
            new BoundedTextCaptureStream(Encoding.UTF8, destination, 100);

        sut.Write(Encoding.UTF8.GetBytes("final"));

        sut.Complete();

        destination.GetSnapshot()
            .Should()
            .Be($"final{Environment.NewLine}");
    }

    [Fact]
    public void Complete_WhenCalledTwice_ShouldBeIdempotent()
    {
        var destination = new SlidingWindowTextBuffer(10, 100);
        using var sut =
            new BoundedTextCaptureStream(Encoding.UTF8, destination, 100);

        sut.Write(Encoding.UTF8.GetBytes("final"));

        sut.Complete();
        Action act = sut.Complete;

        act.Should().NotThrow();
        destination.GetSnapshot()
            .Should()
            .Be($"final{Environment.NewLine}");
    }

    [Fact]
    public void Write_WhenStreamHasCompleted_ShouldThrow()
    {
        var destination = new SlidingWindowTextBuffer(10, 100);
        using var sut =
            new BoundedTextCaptureStream(Encoding.UTF8, destination, 100);

        sut.Complete();

        Action act = () => sut.Write(Encoding.UTF8.GetBytes("late"));

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*already been completed*");
    }

    [Fact]
    public void CanWrite_AfterComplete_ShouldBeFalse()
    {
        var destination = new SlidingWindowTextBuffer(10, 100);
        using var sut =
            new BoundedTextCaptureStream(Encoding.UTF8, destination, 100);

        sut.CanWrite.Should().BeTrue();

        sut.Complete();

        sut.CanWrite.Should().BeFalse();
    }

    [Fact]
    public void CanWrite_AfterDispose_ShouldBeFalse()
    {
        var destination = new SlidingWindowTextBuffer(10, 100);
        var sut =
            new BoundedTextCaptureStream(Encoding.UTF8, destination, 100);

        sut.Dispose();

        sut.CanWrite.Should().BeFalse();
    }

    [Fact]
    public void Length_WhenAccessed_ShouldThrow()
    {
        var destination = new SlidingWindowTextBuffer(10, 100);
        using var sut =
            new BoundedTextCaptureStream(Encoding.UTF8, destination, 100);

        Action act = () => _ = sut.Length;

        act.Should()
            .Throw<NotSupportedException>()
            .WithMessage("*Length is not supported*");
    }

    [Fact]
    public void Position_WhenAccessed_ShouldThrow()
    {
        var destination = new SlidingWindowTextBuffer(10, 100);
        using var sut =
            new BoundedTextCaptureStream(Encoding.UTF8, destination, 100);

        Action act = () => _ = sut.Position;

        act.Should()
            .Throw<NotSupportedException>()
            .WithMessage("*Position is not supported*");
    }

    [Fact]
    public async Task WriteAsync_WhenCancellationIsRequested_ShouldThrow()
    {
        var destination = new SlidingWindowTextBuffer(10, 100);
        using var sut =
            new BoundedTextCaptureStream(Encoding.UTF8, destination, 100);
        using var cancellationTokenSource = new CancellationTokenSource();

        cancellationTokenSource.Cancel();

        Func<Task> act = async () =>
            await sut.WriteAsync(
                Encoding.UTF8.GetBytes("data"),
                cancellationTokenSource.Token);

        await act.Should()
            .ThrowAsync<OperationCanceledException>()
            .WithMessage("*canceled*");
    }

    [Fact]
    public async Task Write_WhenCalledConcurrently_ShouldSerializeWritesWithoutCorruptingState()
    {
        const int writeCount = 100;
        var destination = new SlidingWindowTextBuffer(writeCount, 10_000);
        using var sut =
            new BoundedTextCaptureStream(Encoding.UTF8, destination, 100);

        Task[] writes = Enumerable
            .Range(0, writeCount)
            .Select(index => Task.Run(() =>
                sut.Write(Encoding.UTF8.GetBytes($"line-{index:D3}\n"))))
            .ToArray();

        await Task.WhenAll(writes);
        sut.Complete();

        string[] lines = destination
            .GetSnapshot()
            .Split(
                Environment.NewLine,
                StringSplitOptions.RemoveEmptyEntries);

        lines.Should()
            .HaveCount(writeCount)
            .And.OnlyHaveUniqueItems();
    }
}
