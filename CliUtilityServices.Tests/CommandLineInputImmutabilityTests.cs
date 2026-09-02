using CliUtilityServices.Security;
using FluentAssertions;

namespace CliUtilityServices.Tests;

/// <summary>
/// Verifies immutable snapshot behavior for <see cref="CommandLineInput"/>.
/// </summary>
public sealed class CommandLineInputImmutabilityTests
{
    /// <summary>
    /// Verifies that mutating the source environment dictionary after construction
    /// does not mutate the command-line input.
    /// </summary>
    [Fact]
    public void EnvironmentVariables_WhenSourceDictionaryIsMutated_ShouldRemainUnchanged()
    {
        var source =
            new Dictionary<string, string?>
            {
                ["TEST_VALUE"] = "before"
            };

        var sut =
            new CommandLineInput
            {
                Command = "test-command",
                EnvironmentVariables = source
            };

        source["TEST_VALUE"] = "after";
        source["ADDED_LATER"] = "value";

        sut.EnvironmentVariables["TEST_VALUE"]
            .Should()
            .Be("before");

        sut.EnvironmentVariables
            .Should()
            .NotContainKey("ADDED_LATER");
    }

    /// <summary>
    /// Verifies that a null environment dictionary is rejected by the public type.
    /// </summary>
    [Fact]
    public void EnvironmentVariables_WhenNull_ShouldThrowArgumentNullException()
    {
        Action act =
            () =>
            {
                _ =
                    new CommandLineInput
                    {
                        Command = "test-command",
                        EnvironmentVariables = null!
                    };
            };

        act.Should()
            .Throw<ArgumentNullException>()
            .WithMessage("*value*");
    }

    /// <summary>
    /// Verifies that the builder can retain an immutable environment policy reference
    /// without exposing mutable collection aliases.
    /// </summary>
    [Fact]
    public void WithEnvironmentPolicy_WhenSourceCollectionsAreLaterMutated_ShouldKeepPolicySnapshot()
    {
        var allowedVariables =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "SAFE_VALUE"
            };

        var policy =
            new ChildEnvironmentPolicy
            {
                Mode = ChildEnvironmentMode.AllowList,
                AllowedVariables = allowedVariables
            };

        var sut =
            new CommandLineInputBuilder()
                .WithCommand("test-command")
                .WithEnvironmentPolicy(policy)
                .Build();

        allowedVariables.Add("MUTATED_LATER");

        sut.EnvironmentPolicy.AllowedVariables
            .Should()
            .Contain("SAFE_VALUE");

        sut.EnvironmentPolicy.AllowedVariables
            .Should()
            .NotContain("MUTATED_LATER");
    }
}
