using FluentAssertions;

namespace CliUtilityServices.Tests;

public class CommandLineInputBuilderTests
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
}
