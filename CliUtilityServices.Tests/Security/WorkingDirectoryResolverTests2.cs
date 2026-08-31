using CliUtilityServices.Security;
using EnvironmentUtilityServices;
using FluentAssertions;
using Moq;
using SymbolicLinkUtilityServices.Security;
using System.IO.Abstractions;

namespace CliUtilityServices.Tests.Security;

public sealed partial class WorkingDirectoryResolverTests
{
    [Fact]
    public void Resolve_WhenTrustedDescendantIsValid_ShouldInvokePathLinkValidator()
    {
        const string trustedRoot =
            @"C:\trusted";

        const string normalizedTrustedRoot =
            @"C:\trusted\";

        const string workingDirectory =
            @"C:\trusted\work";

        const string normalizedWorkingDirectory =
            @"C:\trusted\work\";

        var fileSystem =
            new Mock<IFileSystem>(
                MockBehavior.Strict);

        var path =
            new Mock<IPath>(
                MockBehavior.Strict);

        var directory =
            new Mock<IDirectory>(
                MockBehavior.Strict);

        var osUtilityService =
            new Mock<IOsUtilityService>(
                MockBehavior.Strict);

        var pathLinkValidator =
            new Mock<IPathLinkValidator>(
                MockBehavior.Strict);

        fileSystem
            .SetupGet(system => system.Path)
            .Returns(path.Object);

        fileSystem
            .SetupGet(system => system.Directory)
            .Returns(directory.Object);

        path
            .Setup(item =>
                item.IsPathFullyQualified(
                    trustedRoot))
            .Returns(true);

        path
            .Setup(item =>
                item.IsPathFullyQualified(
                    workingDirectory))
            .Returns(true);

        path
            .Setup(item =>
                item.GetFullPath(
                    trustedRoot))
            .Returns(trustedRoot);

        path
            .Setup(item =>
                item.GetFullPath(
                    workingDirectory))
            .Returns(workingDirectory);

        path
            .SetupGet(item =>
                item.DirectorySeparatorChar)
            .Returns('\\');

        directory
            .Setup(item =>
                item.Exists(
                    trustedRoot))
            .Returns(true);

        directory
            .Setup(item =>
                item.Exists(
                    workingDirectory))
            .Returns(true);

        osUtilityService
            .Setup(service =>
                service.GetComparison())
            .Returns(
                StringComparison.OrdinalIgnoreCase);

        pathLinkValidator
            .Setup(validator =>
                validator.ValidateNoPathIndirection(
                    normalizedTrustedRoot,
                    normalizedTrustedRoot));

        pathLinkValidator
            .Setup(validator =>
                validator.ValidateNoPathIndirection(
                    normalizedTrustedRoot,
                    normalizedWorkingDirectory));

        var sut =
            new WorkingDirectoryResolver(
                fileSystem.Object,
                osUtilityService.Object,
                trustedRoot,
                pathLinkValidator.Object);

        string result =
            sut.Resolve(
                workingDirectory);

        result.Should().Be(
            workingDirectory);

        pathLinkValidator.Verify(
            validator =>
                validator.ValidateNoPathIndirection(
                    normalizedTrustedRoot,
                    normalizedWorkingDirectory),
            Times.Once);
    }
}