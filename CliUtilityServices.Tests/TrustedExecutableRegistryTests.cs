using CliUtilityServices.Security;
using FluentAssertions;
using Moq;

namespace CliUtilityServices.Security.Tests;

public sealed class TrustedExecutableRegistryTests
{
    [Fact]
    public void Resolve_WhenExecutableIsRegistered_ShouldStillValidateConfiguredPath()
    {
        var pathResolver =
            new Mock<IExecutableResolver>(
                MockBehavior.Strict);

        var executables =
            new Dictionary<string, string>
            {
                ["git"] = @"C:\Program Files\Git\cmd\git.exe"
            };

        pathResolver
            .Setup(x => x.Resolve(
                @"C:\Program Files\Git\cmd\git.exe"))
            .Returns(
                @"C:\Program Files\Git\cmd\git.exe");

        var sut =
            new TrustedExecutableRegistry(
                executables,
                pathResolver.Object);

        string actual = sut.Resolve("git");

        actual.Should()
            .Be(@"C:\Program Files\Git\cmd\git.exe");

        pathResolver.Verify(
            x => x.Resolve(
                @"C:\Program Files\Git\cmd\git.exe"),
            Times.Once);
    }

    [Fact]
    public void Resolve_WhenExecutableIsNotRegistered_ShouldThrowNotSupportedException()
    {
        var pathResolver =
            new Mock<IExecutableResolver>(
                MockBehavior.Strict);

        var sut =
            new TrustedExecutableRegistry(
                new Dictionary<string, string>(),
                pathResolver.Object);

        Action act = () => sut.Resolve("untrusted-tool");

        act.Should()
            .Throw<NotSupportedException>()
            .WithMessage(
                "*untrusted-tool*not registered as trusted*");

        pathResolver.Verify(
            x => x.Resolve(It.IsAny<string>()),
            Times.Never);
    }
}