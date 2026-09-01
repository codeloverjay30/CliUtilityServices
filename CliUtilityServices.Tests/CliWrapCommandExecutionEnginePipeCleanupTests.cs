using CliUtilityServices.Pipes;
using CliUtilityServices.Security;
using CliWrap;
using FluentAssertions;
using Moq;
using Xunit;

namespace CliUtilityServices.Tests;

public sealed class CliWrapCommandExecutionEnginePipeCleanupTests
{
    [Fact]
    public void ExecuteAsync_WhenPipeConfigurationFails_ShouldInvokeExecutionScopedCleanup()
    {
        const string executablePath = "fake-tool";
        const string expectedMessage = "pipe configuration failed";

        var executableResolver =
            new Mock<IExecutableResolver>(
                MockBehavior.Strict);

        var environmentResolver =
            new Mock<IChildEnvironmentResolver>(
                MockBehavior.Strict);

        var workingDirectoryResolver =
            new Mock<IWorkingDirectoryResolver>(
                MockBehavior.Strict);

        var pipeStrategy =
            new Mock<ICommandPipeStrategy>(
                MockBehavior.Strict);

        Mock<IExecutionScopedPipeStrategy> executionScopedPipeStrategy =
            pipeStrategy.As<IExecutionScopedPipeStrategy>();

        executableResolver
            .Setup(resolver =>
                resolver.Resolve(
                    executablePath))
            .Returns(executablePath);

        environmentResolver
            .Setup(resolver =>
                resolver.Resolve(
                    It.IsAny<ChildEnvironmentPolicy>(),
                    It.IsAny<IReadOnlyDictionary<string, string?>>()))
            .Returns(
                new Dictionary<string, string?>());

        pipeStrategy
            .Setup(strategy =>
                strategy.ConfigurePipes(
                    It.IsAny<Command>(),
                    It.IsAny<System.Text.Encoding>()))
            .Throws(
                new InvalidOperationException(
                    expectedMessage));

        executionScopedPipeStrategy
            .Setup(strategy =>
                strategy.CleanupAsync())
            .Returns(Task.CompletedTask);

        var sut =
            new CliWrapCommandExecutionEngine(
                executableResolver.Object,
                environmentResolver.Object,
                workingDirectoryResolver.Object);

        var input =
            new CommandLineInput
            {
                Command = executablePath,
                PipeStrategy = pipeStrategy.Object
            };

        Action act =
            () => sut.ExecuteAsync(
                    input,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                $"*{expectedMessage}*");

        executionScopedPipeStrategy.Verify(
            strategy =>
                strategy.CleanupAsync(),
            Times.Once);

        workingDirectoryResolver.Verify(
            resolver =>
                resolver.Resolve(
                    It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public void ExecuteAsync_WhenPrimaryAndCleanupOperationsFail_ShouldPreservePrimaryException()
    {
        const string executablePath = "fake-tool";
        const string primaryMessage = "pipe configuration failed";
        const string cleanupMessage = "pipe cleanup failed";
        const string cleanupExceptionDataKey =
            "CliUtilityServices.PipeCleanupException";

        var executableResolver =
            new Mock<IExecutableResolver>(
                MockBehavior.Strict);

        var environmentResolver =
            new Mock<IChildEnvironmentResolver>(
                MockBehavior.Strict);

        var workingDirectoryResolver =
            new Mock<IWorkingDirectoryResolver>(
                MockBehavior.Strict);

        var pipeStrategy =
            new Mock<ICommandPipeStrategy>(
                MockBehavior.Strict);

        Mock<IExecutionScopedPipeStrategy> executionScopedPipeStrategy =
            pipeStrategy.As<IExecutionScopedPipeStrategy>();

        executableResolver
            .Setup(resolver =>
                resolver.Resolve(
                    executablePath))
            .Returns(executablePath);

        environmentResolver
            .Setup(resolver =>
                resolver.Resolve(
                    It.IsAny<ChildEnvironmentPolicy>(),
                    It.IsAny<IReadOnlyDictionary<string, string?>>()))
            .Returns(
                new Dictionary<string, string?>());

        pipeStrategy
            .Setup(strategy =>
                strategy.ConfigurePipes(
                    It.IsAny<Command>(),
                    It.IsAny<System.Text.Encoding>()))
            .Throws(
                new InvalidOperationException(
                    primaryMessage));

        executionScopedPipeStrategy
            .Setup(strategy =>
                strategy.CleanupAsync())
            .ThrowsAsync(
                new IOException(
                    cleanupMessage));

        var sut =
            new CliWrapCommandExecutionEngine(
                executableResolver.Object,
                environmentResolver.Object,
                workingDirectoryResolver.Object);

        var input =
            new CommandLineInput
            {
                Command = executablePath,
                PipeStrategy = pipeStrategy.Object
            };

        Action act =
            () => sut.ExecuteAsync(
                    input,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

        var assertion =
            act.Should()
                .Throw<InvalidOperationException>()
                .WithMessage(
                    $"*{primaryMessage}*");

        assertion.Which.Data
            .Contains(
                cleanupExceptionDataKey)
            .Should()
            .BeTrue();

        assertion.Which.Data[
                cleanupExceptionDataKey]
            .Should()
            .BeOfType<IOException>()
            .Which.Message.Should()
            .Be(cleanupMessage);

        executionScopedPipeStrategy.Verify(
            strategy =>
                strategy.CleanupAsync(),
            Times.Once);
    }

    [Fact]
    public void ExecuteAsync_WhenPipeStrategyIsNotExecutionScopedAndConfigurationFails_ShouldPreservePrimaryException()
    {
        const string executablePath = "fake-tool";
        const string expectedMessage = "pipe configuration failed";

        var executableResolver =
            new Mock<IExecutableResolver>(
                MockBehavior.Strict);

        var environmentResolver =
            new Mock<IChildEnvironmentResolver>(
                MockBehavior.Strict);

        var workingDirectoryResolver =
            new Mock<IWorkingDirectoryResolver>(
                MockBehavior.Strict);

        var pipeStrategy =
            new Mock<ICommandPipeStrategy>(
                MockBehavior.Strict);

        executableResolver
            .Setup(resolver =>
                resolver.Resolve(
                    executablePath))
            .Returns(executablePath);

        environmentResolver
            .Setup(resolver =>
                resolver.Resolve(
                    It.IsAny<ChildEnvironmentPolicy>(),
                    It.IsAny<IReadOnlyDictionary<string, string?>>()))
            .Returns(
                new Dictionary<string, string?>());

        pipeStrategy
            .Setup(strategy =>
                strategy.ConfigurePipes(
                    It.IsAny<Command>(),
                    It.IsAny<System.Text.Encoding>()))
            .Throws(
                new InvalidOperationException(
                    expectedMessage));

        var sut =
            new CliWrapCommandExecutionEngine(
                executableResolver.Object,
                environmentResolver.Object,
                workingDirectoryResolver.Object);

        var input =
            new CommandLineInput
            {
                Command = executablePath,
                PipeStrategy = pipeStrategy.Object
            };

        Action act =
            () => sut.ExecuteAsync(
                    input,
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                $"*{expectedMessage}*");
    }
}
