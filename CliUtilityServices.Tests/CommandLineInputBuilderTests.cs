using FluentAssertions;
using Moq;
using EnvironmentUtilityServices;

namespace CliUtilityServices.Tests;

public class CommandLineInputBuilderTests
{
    [Fact]
    public void WithTimeout_WhenTimeoutIsZero_ShouldThrowArgumentOutOfRangeException()
    {
        var builder = new CommandLineInputBuilder();

        Action act = () =>
            builder.WithTimeout(TimeSpan.Zero);

        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Timeout must be greater than zero*");
    }

    [Fact]
    public void WithTimeout_WhenTimeoutIsNegative_ShouldThrowArgumentOutOfRangeException()
    {
        var builder = new CommandLineInputBuilder();

        Action act = () =>
            builder.WithTimeout(TimeSpan.FromMilliseconds(-1));

        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Timeout must be greater than zero*");
    }

    [Fact]
    public void WithTimeout_WhenTimeoutIsPositive_ShouldStoreTimeout()
    {
        TimeSpan expectedTimeout = TimeSpan.FromSeconds(30);

        var environmentService = new Mock<IEnvironmentService>(
            MockBehavior.Strict);

        environmentService
            .Setup(x => x.IsWindows())
            .Returns(false);

        CommandLineInput input = new CommandLineInputBuilder()
            .WithEnvironmentService(environmentService.Object)
            .WithCommand("dotnet")
            .WithArguments(["--version"])
            .WithTimeout(expectedTimeout)
            .Build();

        input.Timeout.Should().Be(expectedTimeout);
    }

    [Fact]
    public void Build_WhenCommandIsEmpty_ShouldThrowArgumentException()
    {
        var environmentService = new Mock<IEnvironmentService>(
            MockBehavior.Strict);

        var builder = new CommandLineInputBuilder()
            .WithEnvironmentService(environmentService.Object);

        Action act = () => builder.Build();

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage("*command*");
    }

    [Fact]
    public void WithArguments_WhenArgumentsIsNull_ShouldThrowArgumentNullException()
    {
        var builder = new CommandLineInputBuilder();

        Action act = () =>
            builder.WithArguments(null!);

        act.Should()
            .Throw<ArgumentNullException>()
            .WithMessage("*arguments*");
    }

    [Fact]
    public void AddArgument_WhenArgumentIsNull_ShouldThrowArgumentNullException()
    {
        var builder = new CommandLineInputBuilder();

        Action act = () =>
            builder.AddArgument(null!);

        act.Should()
            .Throw<ArgumentNullException>()
            .WithMessage("*argument*");
    }

    [Fact]
    public void Build_ShouldPreserveArgumentBoundaries()
    {
        string suspiciousArgument =
            "value; rm -rf / && echo injected";

        var environmentService = new Mock<IEnvironmentService>(
            MockBehavior.Strict);

        environmentService
            .Setup(x => x.IsWindows())
            .Returns(false);

        CommandLineInput input = new CommandLineInputBuilder()
            .WithEnvironmentService(environmentService.Object)
            .WithCommand("example")
            .WithArguments(
            [
                "--value",
                suspiciousArgument
            ])
            .Build();

        input.Arguments.Should().Equal(
        [
            "--value",
            suspiciousArgument
        ]);
    }
}