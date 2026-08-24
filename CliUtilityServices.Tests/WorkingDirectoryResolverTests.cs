using System.IO.Abstractions;
using EnvironmentUtilityServices;
using FluentAssertions;
using Moq;

namespace CliUtilityServices.Security.Tests;

public sealed class WorkingDirectoryResolverTests
{
    private readonly Mock<IFileSystem> _fileSystem;
    private readonly Mock<IPath> _path;
    private readonly Mock<IDirectory> _directory;
    private readonly Mock<IOsUtilityService> _osUtilityService;

    public WorkingDirectoryResolverTests()
    {
        _fileSystem =
            new Mock<IFileSystem>(
                MockBehavior.Strict);

        _path =
            new Mock<IPath>(
                MockBehavior.Strict);

        _directory =
            new Mock<IDirectory>(
                MockBehavior.Strict);

        _osUtilityService =
            new Mock<IOsUtilityService>(
                MockBehavior.Strict);

        _fileSystem
            .SetupGet(x => x.Path)
            .Returns(_path.Object);

        _fileSystem
            .SetupGet(x => x.Directory)
            .Returns(_directory.Object);

        _osUtilityService
            .Setup(x => x.GetComparison())
            .Returns(
                StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_WhenFileSystemIsNull_ShouldThrowArgumentNullException()
    {
        Action act = () =>
            new WorkingDirectoryResolver(
                null!,
                _osUtilityService.Object);

        act.Should()
            .Throw<ArgumentNullException>()
            .WithMessage("*fileSystem*");
    }

    [Fact]
    public void Constructor_WhenOsUtilityServiceIsNull_ShouldThrowArgumentNullException()
    {
        Action act = () =>
            new WorkingDirectoryResolver(
                _fileSystem.Object,
                null!);

        act.Should()
            .Throw<ArgumentNullException>()
            .WithMessage("*osUtilityService*");
    }

    [Fact]
    public void Resolve_WhenWorkingDirectoryIsRelative_ShouldThrowInvalidOperationException()
    {
        const string workingDirectory =
            @"..\unsafe";

        _path
            .Setup(x => x.IsPathFullyQualified(
                workingDirectory))
            .Returns(false);

        var sut =
            new WorkingDirectoryResolver(
                _fileSystem.Object,
                _osUtilityService.Object);

        Action act = () =>
            sut.Resolve(workingDirectory);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*must be an absolute path*");
    }

    [Fact]
    public void Resolve_WhenDirectoryDoesNotExist_ShouldThrowDirectoryNotFoundException()
    {
        const string workingDirectory =
            @"C:\Workspace\Missing";

        _path
            .Setup(x => x.IsPathFullyQualified(
                workingDirectory))
            .Returns(true);

        _path
            .Setup(x => x.GetFullPath(
                workingDirectory))
            .Returns(workingDirectory);

        _directory
            .Setup(x => x.Exists(
                workingDirectory))
            .Returns(false);

        var sut =
            new WorkingDirectoryResolver(
                _fileSystem.Object,
                _osUtilityService.Object);

        Action act = () =>
            sut.Resolve(workingDirectory);

        act.Should()
            .Throw<DirectoryNotFoundException>()
            .WithMessage(
                "*C:\\Workspace\\Missing*does not exist*");
    }

    [Fact]
    public void Resolve_WhenDirectoryIsValid_ShouldReturnCanonicalPath()
    {
        const string requested =
            @"C:\Workspace\Project\..\Project";

        const string canonical =
            @"C:\Workspace\Project";

        _path
            .Setup(x => x.IsPathFullyQualified(
                requested))
            .Returns(true);

        _path
            .Setup(x => x.GetFullPath(
                requested))
            .Returns(canonical);

        _directory
            .Setup(x => x.Exists(
                canonical))
            .Returns(true);

        var sut =
            new WorkingDirectoryResolver(
                _fileSystem.Object,
                _osUtilityService.Object);

        string actual =
            sut.Resolve(requested);

        actual.Should()
            .Be(canonical);
    }
}