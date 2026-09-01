using System.IO.Abstractions;
using CliUtilityServices.Pipes;
using FluentAssertions;
using Moq;
using Xunit;

namespace CliUtilityServices.Tests.Pipes;

public sealed class FileOutputStreamFactorySecurityTests
{
    [Fact]
    public void Create_WhenFileAlreadyExists_ShouldUseExclusiveCreateNewWriteMode()
    {
        const string filePath =
            "/tmp/cli-output.tmp";

        const string expectedMessage =
            "The temporary output file already exists.";

        var fileSystem =
            new Mock<IFileSystem>(
                MockBehavior.Strict);

        var file =
            new Mock<IFile>(
                MockBehavior.Strict);

        fileSystem
            .SetupGet(
                system =>
                    system.File)
            .Returns(
                file.Object);

        file
            .Setup(
                item =>
                    item.Open(
                        filePath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None))
            .Throws(
                new IOException(
                    expectedMessage));

        var sut =
            new FileOutputStreamFactory(
                fileSystem.Object);

        Action act =
            () => sut.Create(
                filePath);

        act.Should()
            .Throw<IOException>()
            .WithMessage(
                $"*{expectedMessage}*");

        file.Verify(
            item =>
                item.Open(
                    filePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None),
            Times.Once);

        file.VerifyNoOtherCalls();
    }
}