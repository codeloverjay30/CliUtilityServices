using FluentAssertions;

namespace CliUtilityServices.Tests;

/// <summary>
/// Verifies immutable argument snapshot behavior for <see cref="CommandLineInput"/>.
/// </summary>
public sealed class CommandLineInputArgumentsImmutabilityTests
{
    /// <summary>
    /// Verifies that mutating the source list does not change stored arguments.
    /// </summary>
    [Fact]
    public void Arguments_WhenSourceListIsMutated_ShouldRemainUnchanged()
    {
        var source =
            new List<string>
            {
                "--mode",
                "safe"
            };

        var sut =
            new CommandLineInput
            {
                Command = "test-command",
                Arguments = source
            };

        source[1] = "mutated";
        source.Add("--later");

        sut.Arguments
            .Should()
            .Equal("--mode", "safe");
    }

    /// <summary>
    /// Verifies that a deferred enumerable is materialized during initialization.
    /// </summary>
    [Fact]
    public void Arguments_WhenSourceIsDeferred_ShouldSnapshotAtInitialization()
    {
        var source =
            new List<string>
            {
                "before"
            };

        IEnumerable<string> deferred =
            source.Select(
                argument => argument);

        var sut =
            new CommandLineInput
            {
                Command = "test-command",
                Arguments = deferred
            };

        source[0] = "after";

        sut.Arguments
            .Should()
            .Equal("before");
    }

    /// <summary>
    /// Verifies that a null argument collection is rejected.
    /// </summary>
    [Fact]
    public void Arguments_WhenNull_ShouldThrowArgumentNullException()
    {
        Action act =
            () =>
            {
                _ =
                    new CommandLineInput
                    {
                        Command = "test-command",
                        Arguments = null!
                    };
            };

        act.Should()
            .Throw<ArgumentNullException>()
            .WithMessage("*value*");
    }

    /// <summary>
    /// Verifies that null argument elements are rejected.
    /// </summary>
    [Fact]
    public void Arguments_WhenContainingNull_ShouldThrowArgumentException()
    {
        Action act =
            () =>
            {
                _ =
                    new CommandLineInput
                    {
                        Command = "test-command",
                        Arguments =
                        [
                            "--mode",
                            null!
                        ]
                    };
            };

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage("*cannot contain null values*");
    }
}
