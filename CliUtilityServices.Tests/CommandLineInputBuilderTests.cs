using FluentAssertions;

namespace CliUtilityServices.Tests;

public partial class CommandLineInputBuilderTests
{
    [Fact]
    public void Build_WhenCommandIsNull_ShouldThrowArgumentException()
    {
        // Act
        Action act = () => new CommandLineInputBuilder().Build();

        // Assert
        act.Should().Throw<ArgumentException>()
           .Where(p => p.ParamName == "_command");
    }

    [Fact]
    public void WithTimeout_WhenTimeoutIsZero_ShouldThrowArgumentOutOfRangeException()
    {
        var builder = new CommandLineInputBuilder();

        Action act = () => builder.WithTimeout(TimeSpan.Zero);

        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Timeout must be greater than zero*");
    }

    [Fact]
    public void WithTimeout_WhenTimeoutIsNegative_ShouldThrowArgumentOutOfRangeException()
    {
        var builder = new CommandLineInputBuilder();

        Action act = () =>
            builder.WithTimeout(TimeSpan.FromSeconds(-1));

        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Timeout must be greater than zero*");
    }
}
