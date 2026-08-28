using CliUtilityServices.Pipes;
using FluentAssertions;

namespace CliUtilityServices.Tests.Pipes;

public sealed partial class BoundedWriteStreamTests2
{
    [Fact]
    public void Write_WhenUnderlyingStreamFails_ShouldRetainConsumedQuota()
    {
        var innerStream =
            new ThrowingWriteStream(
                new IOException(
                    "Simulated write failure."));

        using var sut =
            new BoundedWriteStream(
                innerStream,
                maximumBytes: 10,
                streamName: "standard output");

        byte[] buffer =
            new byte[6];

        Action act =
            () => sut.Write(
                buffer);

        act.Should()
            .Throw<IOException>()
            .WithMessage(
                "*Simulated write failure*");

        sut.ConsumedQuotaBytes.Should()
            .Be(6);
    }

    [Fact]
    public void Write_WhenPreviousWriteFailed_ShouldNotReuseConsumedQuota()
    {
        var innerStream =
            new ThrowingWriteStream(
                new IOException(
                    "Simulated write failure."));

        using var sut =
            new BoundedWriteStream(
                innerStream,
                maximumBytes: 10,
                streamName: "standard output");

        Action firstAct =
            () => sut.Write(
                new byte[6]);

        firstAct.Should()
            .Throw<IOException>()
            .WithMessage(
                "*Simulated write failure*");

        Action secondAct =
            () => sut.Write(
                new byte[5]);

        secondAct.Should()
            .Throw<OutputLimitExceededException>()
            .WithMessage(
                "*standard output*10*11*");

        sut.ConsumedQuotaBytes.Should()
            .Be(6);
    }

    [Fact]
    public async Task WriteAsync_WhenUnderlyingStreamFails_ShouldRetainConsumedQuota()
    {
        var innerStream =
            new ThrowingWriteStream(
                new IOException(
                    "Simulated async write failure."));

        await using var sut =
            new BoundedWriteStream(
                innerStream,
                maximumBytes: 10,
                streamName: "standard error");

        Func<Task> act =
            () => sut.WriteAsync(
                    new byte[6],
                    0,
                    6,
                    CancellationToken.None);

        await act.Should()
            .ThrowAsync<IOException>()
            .WithMessage(
                "*Simulated async write failure*");

        sut.ConsumedQuotaBytes.Should()
            .Be(6);
    }
}
