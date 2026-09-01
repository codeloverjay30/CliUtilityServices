using System.IO.Abstractions;
using CliUtilityServices.Security;
using FluentAssertions;
using Moq;

namespace CliUtilityServices.Security.Tests;

public sealed partial class TrustedExecutableRegistryTests
{
    [Fact]
    public void Constructor_WhenRegisteredExecutablePathIsRelative_ShouldThrowArgumentException()
    {
        const string executablePath = "git";

        var pathResolver =
            new Mock<IExecutableResolver>(
                MockBehavior.Strict);

        var fileSystem =
            new Mock<IFileSystem>(
                MockBehavior.Strict);

        var path =
            new Mock<IPath>(
                MockBehavior.Strict);

        fileSystem
            .SetupGet(x => x.Path)
            .Returns(path.Object);

        path
            .Setup(x => x.IsPathFullyQualified(
                executablePath))
            .Returns(false);

        var executables =
            new Dictionary<string, string>
            {
                ["git"] = executablePath
            };

        Action act =
            () => new TrustedExecutableRegistry(
                executables,
                pathResolver.Object,
                fileSystem.Object);

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage(
                "*executable path registered for 'git'*must be fully qualified*");

        pathResolver.Verify(
            x => x.Resolve(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public void Constructor_WhenRegisteredExecutablePathIsFullyQualified_ShouldNotThrow()
    {
        const string executablePath =
            @"C:\Program Files\Git\cmd\git.exe";

        var pathResolver =
            new Mock<IExecutableResolver>(
                MockBehavior.Strict);

        var fileSystem =
            new Mock<IFileSystem>(
                MockBehavior.Strict);

        var path =
            new Mock<IPath>(
                MockBehavior.Strict);

        fileSystem
            .SetupGet(x => x.Path)
            .Returns(path.Object);

        path
            .Setup(x => x.IsPathFullyQualified(
                executablePath))
            .Returns(true);

        var executables =
            new Dictionary<string, string>
            {
                ["git"] = executablePath
            };

        Action act =
            () => new TrustedExecutableRegistry(
                executables,
                pathResolver.Object,
                fileSystem.Object);

        act.Should()
            .NotThrow();

        pathResolver.Verify(
            x => x.Resolve(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public void Resolve_WhenExecutableIsRegistered_ShouldStillValidateConfiguredAbsolutePath()
    {
        const string executablePath =
            @"C:\Program Files\Git\cmd\git.exe";

        var pathResolver =
            new Mock<IExecutableResolver>(
                MockBehavior.Strict);

        var fileSystem =
            new Mock<IFileSystem>(
                MockBehavior.Strict);

        var path =
            new Mock<IPath>(
                MockBehavior.Strict);

        var executables =
            new Dictionary<string, string>
            {
                ["git"] = executablePath
            };

        fileSystem
            .SetupGet(x => x.Path)
            .Returns(path.Object);

        path
            .Setup(x => x.IsPathFullyQualified(
                executablePath))
            .Returns(true);

        pathResolver
            .Setup(x => x.Resolve(
                executablePath))
            .Returns(executablePath);

        var sut =
            new TrustedExecutableRegistry(
                executables,
                pathResolver.Object,
                fileSystem.Object);

        string actual =
            sut.Resolve("git");

        actual.Should()
            .Be(executablePath);

        pathResolver.Verify(
            x => x.Resolve(
                executablePath),
            Times.Once);
    }

    [Fact]
    public void Resolve_WhenExecutableIsNotRegistered_ShouldThrowNotSupportedException()
    {
        var pathResolver =
            new Mock<IExecutableResolver>(
                MockBehavior.Strict);

        var fileSystem =
            new Mock<IFileSystem>(
                MockBehavior.Strict);

        var sut =
            new TrustedExecutableRegistry(
                new Dictionary<string, string>(),
                pathResolver.Object,
                fileSystem.Object);

        Action act =
            () => sut.Resolve(
                "untrusted-tool");

        act.Should()
            .Throw<NotSupportedException>()
            .WithMessage(
                "*untrusted-tool*not registered as trusted*");

        pathResolver.Verify(
            x => x.Resolve(It.IsAny<string>()),
            Times.Never);
    }
}
