using CliUtilityServices.Security;
using FluentAssertions;
using Moq;
using System.IO.Abstractions;

namespace CliUtilityServices.Tests.Security;

public sealed class ExecutableResolverTests
{
    private readonly Mock<IFileSystem> _fileSystem;
    private readonly Mock<IFile> _file;
    private readonly Mock<IPath> _path;
    private readonly Mock<IProcessEnvironmentSource> _environmentSource;

    public ExecutableResolverTests()
    {
        _fileSystem =
            new Mock<IFileSystem>(
                MockBehavior.Strict);

        _file =
            new Mock<IFile>(
                MockBehavior.Strict);

        _path =
            new Mock<IPath>(
                MockBehavior.Strict);

        _environmentSource =
            new Mock<IProcessEnvironmentSource>(
                MockBehavior.Strict);

        _fileSystem
            .SetupGet(x => x.File)
            .Returns(_file.Object);

        _fileSystem
            .SetupGet(x => x.Path)
            .Returns(_path.Object);
    }

    [Fact]
    public void Resolve_WhenAbsolutePathExists_ShouldReturnCanonicalPath()
    {
        const string executable =
            @"D:\Android\platform-tools\adb.exe";

        _path
            .Setup(x =>
                x.IsPathFullyQualified(
                    executable))
            .Returns(true);

        _path
            .Setup(x =>
                x.GetFullPath(
                    executable))
            .Returns(executable);

        _file
            .Setup(x =>
                x.Exists(
                    executable))
            .Returns(true);

        var sut =
            new ExecutableResolver(
                _fileSystem.Object,
                _environmentSource.Object);

        string result =
            sut.Resolve(
                executable);

        result.Should().Be(
            executable);
    }

    [Fact]
    public void Resolve_WhenAbsolutePathIsRequiredAndNameIsRelative_ShouldThrow()
    {
        const string executable = "adb";

        _path
            .Setup(x =>
                x.IsPathFullyQualified(
                    executable))
            .Returns(false);

        var sut =
            new ExecutableResolver(
                _fileSystem.Object,
                _environmentSource.Object,
                ExecutableResolutionMode.RequireAbsolutePath);

        Action act =
            () => sut.Resolve(
                executable);

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*adb*absolute path*");
    }

    [Fact]
    public void Resolve_WhenPathLookupCannotFindExecutable_ShouldThrow()
    {
        const string executable = "adb";

        _path
            .Setup(x =>
                x.IsPathFullyQualified(
                    executable))
            .Returns(false);

        _environmentSource
            .Setup(x =>
                x.GetEnvironmentVariables())
            .Returns(
                new Dictionary<string, string?>
                {
                    ["PATH"] = string.Empty
                });

        var sut =
            new ExecutableResolver(
                _fileSystem.Object,
                _environmentSource.Object,
                ExecutableResolutionMode.PathLookup);

        Action act =
            () => sut.Resolve(
                executable);

        act.Should()
            .Throw<FileNotFoundException>()
            .WithMessage(
                "*adb*PATH*");
    }
}