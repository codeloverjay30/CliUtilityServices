using CliUtilityServices.Security;
using FluentAssertions;

namespace CliUtilityServices.Tests.Security;

public sealed class ChildEnvironmentPolicyBuilderRegressionTests
{
    [Fact]
    public void CreateWithAllowListMode_WhenSourceMutates_ShouldKeepOriginalSnapshot()
    {
        var source =
            new HashSet<string>(
                StringComparer.Ordinal)
            {
                "PATH",
                "HOME"
            };

        ChildEnvironmentPolicy policy =
            ChildEnvironmentPolicyBuilder
                .CreateWithAllowListMode(
                    source);

        source.Clear();
        source.Add(
            "SECRET");

        policy.AllowedVariables.Should()
            .BeEquivalentTo(
                ["PATH", "HOME"]);

        policy.AllowedVariables.Should()
            .NotContain(
                "SECRET");
    }

    [Fact]
    public void CreateWithAllowInheritedListMode_WhenSourceMutates_ShouldKeepOriginalSnapshot()
    {
        var source =
            new HashSet<string>(
                StringComparer.Ordinal)
            {
                "PATH",
                "HOME"
            };

        ChildEnvironmentPolicy policy =
            ChildEnvironmentPolicyBuilder
                .CreateWithAllowInheritedListMode(
                    source);

        source.Clear();
        source.Add(
            "SECRET");

        policy.AllowedInheritedVariables.Should()
            .BeEquivalentTo(
                ["PATH", "HOME"]);

        policy.AllowedInheritedVariables.Should()
            .NotContain(
                "SECRET");
    }

    [Fact]
    public void CreateWithAllowInheritedListMode_WhenBothSourcesMutate_ShouldKeepBothOriginalSnapshots()
    {
        var allowed =
            new HashSet<string>(
                StringComparer.Ordinal)
            {
                "PATH"
            };

        var denied =
            new HashSet<string>(
                StringComparer.Ordinal)
            {
                "SECRET"
            };

        ChildEnvironmentPolicy policy =
            ChildEnvironmentPolicyBuilder
                .CreateWithAllowInheritedListMode(
                    allowed,
                    denied);

        allowed.Clear();
        allowed.Add(
            "MUTATED_ALLOWED");

        denied.Clear();
        denied.Add(
            "MUTATED_DENIED");

        policy.AllowedInheritedVariables.Should()
            .BeEquivalentTo(
                ["PATH"]);

        policy.DeniedVariables.Should()
            .BeEquivalentTo(
                ["SECRET"]);
    }

    [Fact]
    public void CreateWithDenyListMode_WhenSourceMutates_ShouldKeepOriginalSnapshot()
    {
        var source =
            new HashSet<string>(
                StringComparer.Ordinal)
            {
                "SECRET",
                "TOKEN"
            };

        ChildEnvironmentPolicy policy =
            ChildEnvironmentPolicyBuilder
                .CreateWithDenyListMode(
                    source);

        source.Clear();
        source.Add(
            "PATH");

        policy.DeniedVariables.Should()
            .BeEquivalentTo(
                ["SECRET", "TOKEN"]);

        policy.DeniedVariables.Should()
            .NotContain(
                "PATH");
    }

    [Fact]
    public void CreateWithDenyListMode_WhenSourceIsEmpty_ShouldThrowExpectedMessage()
    {
        var source =
            new HashSet<string>(
                StringComparer.Ordinal);

        Action act =
            () => ChildEnvironmentPolicyBuilder
                .CreateWithDenyListMode(
                    source);

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage(
                "*Denied variables cannot be empty.*");
    }
}
