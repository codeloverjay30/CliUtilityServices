using Commands.Infrastructure;
using EnvironmentUtilityServices;
using FluentAssertions;
using Moq;

namespace CliUtilityServices.Tests;

public partial class CliCommandExecutorProcessTests
    : CliCommandExecutorTestBase
{
    [Fact]
    public async Task ExecuteProcessAsync_WhenInputIsValid_ShouldReturnExecutionResult()
    {
        CommandLineInput input = CreateInput(
            command: "dotnet",
            arguments: ["--version"]);

        var expected = new CommandExecutionResult(
            StandardOutput: "10.0.100",
            StandardError: string.Empty,
            ExitCode: 0,
            RunTime: TimeSpan.FromMilliseconds(100));

        ExecutionEngine
            .Setup(x => x.ExecuteAsync(
                input,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        CommandExecutionResult actual =
            await Sut.ExecuteProcessAsync(input);

        actual.Should().Be(expected);

        ExecutionEngine.Verify(
            x => x.ExecuteAsync(
                input,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteProcessAsync_ShouldPreserveArgumentsWithoutConcatenation()
    {
        const string suspiciousArgument =
            "hello; echo injected && whoami";

        CommandLineInput input = CreateInput(
            command: "example",
            arguments:
            [
                "--message",
                suspiciousArgument
            ]);

        var expected = new CommandExecutionResult(
            string.Empty,
            string.Empty,
            0,
            TimeSpan.Zero);

        CommandLineInput? capturedInput = null;

        ExecutionEngine
            .Setup(x => x.ExecuteAsync(
                It.IsAny<CommandLineInput>(),
                It.IsAny<CancellationToken>()))
            .Callback<CommandLineInput, CancellationToken>(
                (actualInput, _) =>
                {
                    capturedInput = actualInput;
                })
            .ReturnsAsync(expected);

        await Sut.ExecuteProcessAsync(input);

        capturedInput.Should().NotBeNull();

        capturedInput!
            .Arguments
            .Should()
            .Equal(
            [
                "--message",
                suspiciousArgument
            ]);
    }

    [Fact]
    public async Task ExecuteProcessAsync_WhenCallerCancels_ShouldPropagateOperationCanceledException()
    {
        CommandLineInput input = CreateInput(
            "dotnet",
            ["--version"]);

        using var cancellationTokenSource =
            new CancellationTokenSource();

        ExecutionEngine
            .Setup(x => x.ExecuteAsync(
                input,
                It.IsAny<CancellationToken>()))
            .Returns(
                async (
                    CommandLineInput _,
                    CancellationToken cancellationToken) =>
                {
                    await Task.Delay(
                        Timeout.InfiniteTimeSpan,
                        cancellationToken);

                    return new CommandExecutionResult(
                        string.Empty,
                        string.Empty,
                        0,
                        TimeSpan.Zero);
                });

        cancellationTokenSource.Cancel();

        Func<Task> act = async () =>
            await Sut.ExecuteProcessAsync(
                input,
                cancellationTokenSource.Token);

        await act.Should()
            .ThrowAsync<OperationCanceledException>()
            .WithMessage("*canceled*");
    }

    private CommandLineInput CreateInput(
        string command,
        IEnumerable<string> arguments,
        TimeSpan? timeout = null)
    {
        EnvironmentService
            .Setup(x => x.IsWindows())
            .Returns(false);

        var builder = new CommandLineInputBuilder()
            .WithEnvironmentService(EnvironmentService.Object)
            .WithCommand(command)
            .WithArguments(arguments);

        if (timeout is not null)
        {
            builder.WithTimeout(timeout.Value);
        }

        return builder.Build();
    }
}