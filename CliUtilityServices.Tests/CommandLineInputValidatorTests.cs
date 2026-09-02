using CliUtilityServices.Pipes;
using CliUtilityServices.Security;
using FluentAssertions;

namespace CliUtilityServices.Tests;

/// <summary>
/// Verifies execution-boundary validation for <see cref="CommandLineInput"/>.
/// </summary>
public sealed class CommandLineInputValidatorTests
{
    /// <summary>
    /// Verifies that a null command-line input is rejected.
    /// </summary>
    [Fact]
    public void ValidateForExecution_WhenInputIsNull_ShouldThrowArgumentNullException()
    {
        Action act =
            () => CommandLineInputValidator.ValidateForExecution(
                null!);

        act.Should()
            .Throw<ArgumentNullException>()
            .WithMessage("*input*");
    }

    /// <summary>
    /// Verifies that a whitespace command is rejected.
    /// </summary>
    [Fact]
    public void ValidateForExecution_WhenCommandIsWhitespace_ShouldThrowArgumentException()
    {
        var input =
            new CommandLineInput
            {
                Command = " "
            };

        Action act =
            () => CommandLineInputValidator.ValidateForExecution(
                input);

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage("*Command*");
    }

    /// <summary>
    /// Verifies that a null pipe strategy is rejected.
    /// </summary>
    [Fact]
    public void ValidateForExecution_WhenPipeStrategyIsNull_ShouldThrowArgumentNullException()
    {
        var input =
            new CommandLineInput
            {
                Command = "test-command",
                PipeStrategy = null!
            };

        Action act =
            () => CommandLineInputValidator.ValidateForExecution(
                input);

        act.Should()
            .Throw<ArgumentNullException>()
            .WithMessage("*PipeStrategy*");
    }

    /// <summary>
    /// Verifies that a null environment policy is rejected.
    /// </summary>
    [Fact]
    public void ValidateForExecution_WhenEnvironmentPolicyIsNull_ShouldThrowArgumentNullException()
    {
        var input =
            new CommandLineInput
            {
                Command = "test-command",
                EnvironmentPolicy = null!
            };

        Action act =
            () => CommandLineInputValidator.ValidateForExecution(
                input);

        act.Should()
            .Throw<ArgumentNullException>()
            .WithMessage("*EnvironmentPolicy*");
    }

    /// <summary>
    /// Verifies that a non-positive timeout is rejected.
    /// </summary>
    [Fact]
    public void ValidateForExecution_WhenTimeoutIsZero_ShouldThrowArgumentOutOfRangeException()
    {
        var input =
            new CommandLineInput
            {
                Command = "test-command",
                Timeout = TimeSpan.Zero
            };

        Action act =
            () => CommandLineInputValidator.ValidateForExecution(
                input);

        act.Should()
            .Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Timeout must be greater than zero*");
    }
}
