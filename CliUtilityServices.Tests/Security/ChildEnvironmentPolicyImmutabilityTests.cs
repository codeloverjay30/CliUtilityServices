using FluentAssertions;

namespace CliUtilityServices.Tests.Security;
public sealed class ChildEnvironmentPolicyImmutabilityTests
{
    [Fact]
    public void Constructor_WhenAllowedInheritedSourceMutates_ShouldKeepSnapshot()
    {
        var source =
            new HashSet<string>(
                StringComparer.Ordinal)
            {
                "PATH",
                "HOME"
            };

        var sut =
            new ChildEnvironmentPolicy
            {
                AllowedInheritedVariables =
                    source
            };

        source.Clear();
        source.Add(
            "SECRET");

        sut.AllowedInheritedVariables.Should()
            .BeEquivalentTo(
                ["PATH", "HOME"]);

        sut.AllowedInheritedVariables.Should()
            .NotContain(
                "SECRET");
    }

    [Fact]
    public void Constructor_WhenAllowedSourceMutates_ShouldKeepSnapshot()
    {
        var source =
            new HashSet<string>(
                StringComparer.Ordinal)
            {
                "PATH"
            };

        var sut =
            new ChildEnvironmentPolicy
            {
                AllowedVariables =
                    source
            };

        source.Clear();
        source.Add(
            "SECRET");

        sut.AllowedVariables.Should()
            .BeEquivalentTo(
                ["PATH"]);

        sut.AllowedVariables.Should()
            .NotContain(
                "SECRET");
    }

    [Fact]
    public void Constructor_WhenDeniedSourceMutates_ShouldKeepSnapshot()
    {
        var source =
            new HashSet<string>(
                StringComparer.Ordinal)
            {
                "SECRET"
            };

        var sut =
            new ChildEnvironmentPolicy
            {
                DeniedVariables =
                    source
            };

        source.Clear();
        source.Add(
            "PATH");

        sut.DeniedVariables.Should()
            .BeEquivalentTo(
                ["SECRET"]);

        sut.DeniedVariables.Should()
            .NotContain(
                "PATH");
    }

    [Fact]
    public void Constructor_WhenOverridesSourceMutates_ShouldKeepSnapshot()
    {
        var source =
            new Dictionary<string, string?>(
                StringComparer.Ordinal)
            {
                ["API_TOKEN"] =
                    "original"
            };

        var sut =
            new ChildEnvironmentPolicy
            {
                Overrides =
                    source
            };

        source["API_TOKEN"] =
            "mutated";

        source["NEW_SECRET"] =
            "value";

        sut.Overrides.Should()
            .ContainKey(
                "API_TOKEN")
            .WhoseValue.Should()
            .Be(
                "original");

        sut.Overrides.Should()
            .NotContainKey(
                "NEW_SECRET");
    }

    [Fact]
    public void Constructor_WhenDeniedVariablesIsNull_ShouldThrowExpectedMessage()
    {
        Action act =
            () =>
            {
                _ =
                    new ChildEnvironmentPolicy
                    {
                        DeniedVariables =
                            null!
                    };
            };

        act.Should()
            .Throw<ArgumentNullException>()
            .WithMessage(
                "*value*");
    }

    [Fact]
    public void Constructor_WhenOverridesIsNull_ShouldThrowExpectedMessage()
    {
        Action act =
            () =>
            {
                _ =
                    new ChildEnvironmentPolicy
                    {
                        Overrides =
                            null!
                    };
            };

        act.Should()
            .Throw<ArgumentNullException>()
            .WithMessage(
                "*value*");
    }
}

