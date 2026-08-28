using System.IO.Abstractions;
using System.Text;
using CliUtilityServices.Pipes;
using FluentAssertions;
using Moq;

namespace CliUtilityServices.Tests.Pipes;

public sealed partial class BoundedWriteStreamTests
{
    [Fact]
    public void Write_WhenWriteFitsWithinLimit_ShouldWriteEntireBuffer()
    {
        // Arrange
        using var innerStream =
            new MemoryStream();

        using var sut =
            new BoundedWriteStream(
                innerStream,
                maximumBytes: 10,
                streamName: "standard output");

        byte[] data =
            Encoding.UTF8.GetBytes(
                "12345");

        // Act
        Action act = () =>
            sut.Write(
                data,
                0,
                data.Length);

        // Assert
        act.Should()
            .NotThrow();

        sut.ConsumedQuotaBytes.Should()
            .Be(5);

        innerStream.Length.Should()
            .Be(5);
    }

    [Fact]
    public void Write_WhenWriteExactlyMatchesLimit_ShouldWriteEntireBuffer()
    {
        // Arrange
        using var innerStream =
            new MemoryStream();

        using var sut =
            new BoundedWriteStream(
                innerStream,
                maximumBytes: 5,
                streamName: "standard output");

        byte[] data =
            Encoding.UTF8.GetBytes(
                "12345");

        // Act
        Action act = () =>
            sut.Write(
                data,
                0,
                data.Length);

        // Assert
        act.Should()
            .NotThrow();

        sut.ConsumedQuotaBytes.Should()
            .Be(5);

        innerStream.Length.Should()
            .Be(5);
    }

    [Fact]
    public void Write_WhenWriteWouldExceedLimit_ShouldThrowOutputLimitExceededException()
    {
        // Arrange
        using var innerStream =
            new MemoryStream();

        using var sut =
            new BoundedWriteStream(
                innerStream,
                maximumBytes: 5,
                streamName: "standard output");

        byte[] first =
            Encoding.UTF8.GetBytes(
                "1234");

        byte[] second =
            Encoding.UTF8.GetBytes(
                "56");

        sut.Write(
            first,
            0,
            first.Length);

        // Act
        Action act = () =>
            sut.Write(
                second,
                0,
                second.Length);

        // Assert
        act.Should()
            .Throw<OutputLimitExceededException>()
            .WithMessage(
                "*standard output*5 bytes*6 bytes*");

        sut.ConsumedQuotaBytes.Should()
            .Be(4);

        innerStream.Length.Should()
            .Be(4);
    }

    [Fact]
    public void Write_WhenWriteWouldExceedLimit_ShouldNotWriteAnyPartOfRejectedBuffer()
    {
        // Arrange
        using var innerStream =
            new MemoryStream();

        using var sut =
            new BoundedWriteStream(
                innerStream,
                maximumBytes: 5,
                streamName: "standard output");

        byte[] accepted =
            "1234"u8.ToArray();

        byte[] rejected =
            "56"u8.ToArray();

        sut.Write(
            accepted,
            0,
            accepted.Length);

        // Act
        Action act = () =>
            sut.Write(
                rejected,
                0,
                rejected.Length);

        // Assert
        act.Should()
            .Throw<OutputLimitExceededException>()
            .WithMessage(
                "*standard output*5 bytes*6 bytes*");

        innerStream.ToArray()
            .Should()
            .Equal(
                accepted);
    }

    [Fact]
    public async Task WriteAsync_WhenWriteFitsWithinLimit_ShouldWriteEntireBuffer()
    {
        // Arrange
        var innerStream =
            new MemoryStream();

        var sut =
            new BoundedWriteStream(
                innerStream,
                maximumBytes: 10,
                streamName: "standard output");

        byte[] data =
            Encoding.UTF8.GetBytes(
                "12345");

        // Act
        Func<Task> act = async () =>
            await sut.WriteAsync(
                data.AsMemory());

        // Assert
        await act.Should()
            .NotThrowAsync();

        sut.ConsumedQuotaBytes.Should()
            .Be(5);

        innerStream.Length.Should()
            .Be(5);

        await sut.DisposeAsync();
    }

    [Fact]
    public async Task WriteAsync_WhenWriteWouldExceedLimit_ShouldThrowOutputLimitExceededException()
    {
        // Arrange
        var innerStream =
            new MemoryStream();

        var sut =
            new BoundedWriteStream(
                innerStream,
                maximumBytes: 4,
                streamName: "standard error");

        byte[] data =
            Encoding.UTF8.GetBytes(
                "12345");

        // Act
        Func<Task> act = async () =>
            await sut.WriteAsync(
                data.AsMemory());

        // Assert
        await act.Should()
            .ThrowAsync<OutputLimitExceededException>()
            .WithMessage(
                "*standard error*4 bytes*5 bytes*");

        sut.ConsumedQuotaBytes.Should()
            .Be(0);

        innerStream.Length.Should()
            .Be(0);

        await sut.DisposeAsync();
    }

    [Fact]
    public void Write_WhenUnderlyingStreamFails_ShouldRetainConsumedQuota()
    {
        // Arrange
        using var innerStream =
            new ThrowingWriteStream(
                new IOException(
                    "Disk write failed."));

        using var sut =
            new BoundedWriteStream(
                innerStream,
                maximumBytes: 100,
                streamName: "standard output");

        byte[] data =
            "12345"u8.ToArray();

        // Act
        Action act = () =>
            sut.Write(
                data,
                0,
                data.Length);

        // Assert
        act.Should()
            .Throw<IOException>()
            .WithMessage(
                "*Disk write failed*");

        sut.ConsumedQuotaBytes.Should()
            .Be(data.Length);
    }

    [Fact]
    public async Task WriteAsync_WhenUnderlyingStreamFails_ShouldRetainConsumedQuota()
    {
        // Arrange
        var innerStream =
            new ThrowingWriteStream(
                new IOException(
                    "Disk write failed."));

        var sut =
            new BoundedWriteStream(
                innerStream,
                maximumBytes: 100,
                streamName: "standard error");

        byte[] data =
            "12345"u8.ToArray();

        // Act
        Func<Task> act = async () =>
            await sut.WriteAsync(
                data.AsMemory());

        // Assert
        await act.Should()
            .ThrowAsync<IOException>()
            .WithMessage(
                "*Disk write failed*");

        sut.ConsumedQuotaBytes.Should()
            .Be(data.Length);

        await sut.DisposeAsync();
    }

    [Fact]
    public void Constructor_WhenMaximumBytesIsZero_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        using var innerStream =
            new MemoryStream();

        // Act
        Action act = () =>
            new BoundedWriteStream(
                innerStream,
                maximumBytes: 0,
                streamName: "standard output");

        // Assert
        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithMessage(
                "*Maximum output bytes must be greater than zero*");
    }

    [Fact]
    public void Constructor_WhenMaximumBytesIsNegative_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        using var innerStream =
            new MemoryStream();

        // Act
        Action act = () =>
            new BoundedWriteStream(
                innerStream,
                maximumBytes: -1,
                streamName: "standard output");

        // Assert
        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithMessage(
                "*Maximum output bytes must be greater than zero*");
    }

    [Fact]
    public void Constructor_WhenInnerStreamIsNotWritable_ShouldThrowArgumentException()
    {
        // Arrange
        using var innerStream =
            new NonWritableStream();

        // Act
        Action act = () =>
            new BoundedWriteStream(
                innerStream,
                maximumBytes: 100,
                streamName: "standard output");

        // Assert
        act.Should()
            .Throw<ArgumentException>()
            .WithMessage(
                "*underlying stream must be writable*");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Constructor_WhenStreamNameIsBlank_ShouldThrowArgumentException(
        string streamName)
    {
        // Arrange
        using var innerStream =
            new MemoryStream();

        // Act
        Action act = () =>
            new BoundedWriteStream(
                innerStream,
                maximumBytes: 100,
                streamName);

        // Assert
        act.Should()
            .Throw<ArgumentException>()
            .WithMessage(
                "*streamName*");
    }
}

public sealed class FileStreamPipeStrategyQuotaTests
{
    [Fact]
    public void Constructor_WhenStandardOutputLimitIsZero_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var fileSystem =
            new Mock<IFileSystem>(
                MockBehavior.Strict);

        // Act
        Action act = () =>
            new FileStreamPipeStrategy(
                fileSystem.Object,
                maxStandardOutputBytes: 0,
                maxStandardErrorBytes: 1024);

        // Assert
        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithMessage(
                "*Maximum output bytes must be greater than zero*");
    }

    [Fact]
    public void Constructor_WhenStandardOutputLimitIsNegative_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var fileSystem =
            new Mock<IFileSystem>(
                MockBehavior.Strict);

        // Act
        Action act = () =>
            new FileStreamPipeStrategy(
                fileSystem.Object,
                maxStandardOutputBytes: -1,
                maxStandardErrorBytes: 1024);

        // Assert
        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithMessage(
                "*Maximum output bytes must be greater than zero*");
    }

    [Fact]
    public void Constructor_WhenStandardErrorLimitIsZero_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var fileSystem =
            new Mock<IFileSystem>(
                MockBehavior.Strict);

        // Act
        Action act = () =>
            new FileStreamPipeStrategy(
                fileSystem.Object,
                maxStandardOutputBytes: 1024,
                maxStandardErrorBytes: 0);

        // Assert
        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithMessage(
                "*Maximum output bytes must be greater than zero*");
    }

    [Fact]
    public void Constructor_WhenStandardErrorLimitIsNegative_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var fileSystem =
            new Mock<IFileSystem>(
                MockBehavior.Strict);

        // Act
        Action act = () =>
            new FileStreamPipeStrategy(
                fileSystem.Object,
                maxStandardOutputBytes: 1024,
                maxStandardErrorBytes: -1);

        // Assert
        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithMessage(
                "*Maximum output bytes must be greater than zero*");
    }

    [Fact]
    public void Constructor_WhenFileSystemIsNull_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () =>
            new FileStreamPipeStrategy(
                null!);

        // Assert
        act.Should()
            .Throw<ArgumentNullException>()
            .WithMessage(
                "*fileSystem*");
    }
}