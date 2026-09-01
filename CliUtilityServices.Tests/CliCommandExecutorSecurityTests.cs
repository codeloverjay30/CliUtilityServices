using System.IO.Abstractions;
using CliUtilityServices;
using EnvironmentUtilityServices;
using FluentAssertions;
using Moq;
using OsVersionUtilityServices;
using Xunit;

namespace CliUtilityServices.Tests;

public sealed class CliCommandExecutorSecurityTests
{
    [Fact]
    public void ExecuteAutoDetectedAsync_WhenOperatingSystemIsUnknown_ShouldFailClosed()
    {
        const string expectedMessage =
            "Automatic terminal detection is not supported on the current operating system.";

        var fileSystem =
            new Mock<IFileSystem>(
                MockBehavior.Strict);

        var environmentService =
            new Mock<IEnvironmentService>(
                MockBehavior.Strict);

        var osVersionResolver =
            new Mock<IOSVersionResolver>(
                MockBehavior.Strict);

        var executionEngine =
            new Mock<ICommandExecutionEngine>(
                MockBehavior.Strict);

        environmentService
            .Setup(
                service =>
                    service.IsWindows())
            .Returns(
                false);

        environmentService
            .Setup(
                service =>
                    service.IsLinux())
            .Returns(
                false);

        environmentService
            .Setup(
                service =>
                    service.IsMacOS())
            .Returns(
                false);

        var sut =
            new CliCommandExecutor(
                fileSystem.Object,
                environmentService.Object,
                osVersionResolver.Object,
                executionEngine.Object);

        var input =
            new CommandLineInput
            {
                Command = "fake-tool"
            };

        Action act =
            () => sut.ExecuteAutoDetectedAsync(
                input);

        act.Should()
            .Throw<PlatformNotSupportedException>()
            .WithMessage(
                $"*{expectedMessage}*");

        osVersionResolver.VerifyNoOtherCalls();
        executionEngine.VerifyNoOtherCalls();
    }
}
