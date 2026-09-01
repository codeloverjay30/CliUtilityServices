using System.IO.Abstractions;
using CliUtilityServices.Security;
using FluentAssertions;
using Moq;

namespace CliUtilityServices.Tests.Security;

public sealed partial class TrustedExecutableRegistryTests
{
    [Fact]
    public void Constructor_WhenRegisteredExecutablePathIsRelative_ShouldThrow()
    {
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
            .SetupGet(system => system.Path)
            .Returns(path.Object);

        path
            .Setup(item =>
                item.IsPathFullyQualified(
                    "adb"))
            .Returns(false);

        var executables =
            new Dictionary<string, string>
            {
                ["adb"] = "adb"
            };

        Action act =
            () => new TrustedExecutableRegistry(
                executables,
                pathResolver.Object,
                fileSystem.Object);

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage(
                "*executable path registered for 'adb'*must be fully qualified*");

        pathResolver.Verify(
            resolver =>
                resolver.Resolve(
                    It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public void Constructor_WhenRegisteredExecutablePathIsFullyQualified_ShouldSucceed()
    {
        const string executablePath =
            @"C:\Android\platform-tools\adb.exe";

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
            .SetupGet(system => system.Path)
            .Returns(path.Object);

        path
            .Setup(item =>
                item.IsPathFullyQualified(
                    executablePath))
            .Returns(true);

        var executables =
            new Dictionary<string, string>
            {
                ["adb"] = executablePath
            };

        Action act =
            () => new TrustedExecutableRegistry(
                executables,
                pathResolver.Object,
                fileSystem.Object);

        act.Should()
            .NotThrow();
    }

    [Fact]
    public void Resolve_WhenExecutableIsRegistered_ShouldDelegatePinnedAbsolutePath()
    {
        const string executablePath =
            @"C:\Android\platform-tools\adb.exe";

        const string canonicalExecutablePath =
            @"C:\Android\platform-tools\adb.exe";

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
            .SetupGet(system => system.Path)
            .Returns(path.Object);

        path
            .Setup(item =>
                item.IsPathFullyQualified(
                    executablePath))
            .Returns(true);

        pathResolver
            .Setup(resolver =>
                resolver.Resolve(
                    executablePath))
            .Returns(
                canonicalExecutablePath);

        var sut =
            new TrustedExecutableRegistry(
                new Dictionary<string, string>
                {
                    ["adb"] = executablePath
                },
                pathResolver.Object,
                fileSystem.Object);

        string result =
            sut.Resolve(
                "adb");

        result.Should()
            .Be(
                canonicalExecutablePath);

        pathResolver.Verify(
            resolver =>
                resolver.Resolve(
                    executablePath),
            Times.Once);
    }

    [Fact]
    public void Resolve_WhenExecutableIsNotRegistered_ShouldThrow()
    {
        const string executablePath =
            @"C:\Android\platform-tools\adb.exe";

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
            .SetupGet(system => system.Path)
            .Returns(path.Object);

        path
            .Setup(item =>
                item.IsPathFullyQualified(
                    executablePath))
            .Returns(true);

        var sut =
            new TrustedExecutableRegistry(
                new Dictionary<string, string>
                {
                    ["adb"] = executablePath
                },
                pathResolver.Object,
                fileSystem.Object);

        Action act =
            () => sut.Resolve(
                "git");

        act.Should()
            .Throw<NotSupportedException>()
            .WithMessage(
                "*Executable 'git' is not registered as trusted.*");

        pathResolver.Verify(
            resolver =>
                resolver.Resolve(
                    It.IsAny<string>()),
            Times.Never);
    }
}