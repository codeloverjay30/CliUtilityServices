using CliUtilityServices.Security;
using EnvironmentUtilityServices;
using FluentAssertions;
using Moq;

namespace CliUtilityServices.Tests.Security;

public sealed partial class ChildEnvironmentResolverInputValidationTests
{
    [Fact]
    public void Resolve_WhenExplicitVariableNameIsWhitespace_ShouldThrow()
    {
        Mock<IProcessEnvironmentSource> environmentSource =
            CreateEnvironmentSource();

        Mock<IOsUtilityService> osUtilityService =
            CreateOsUtilityService();

        var explicitVariables =
            new Dictionary<string, string?>
            {
                ["   "] = "value"
            };

        var sut =
            new ChildEnvironmentResolver(
                environmentSource.Object,
                osUtilityService.Object);

        Action act =
            () => sut.Resolve(
                ChildEnvironmentPolicies.Compatible,
                explicitVariables);

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage(
                "*Environment variable names cannot be null, empty, or whitespace.*");
    }

    [Fact]
    public void Resolve_WhenExplicitVariableNameContainsEquals_ShouldThrow()
    {
        Mock<IProcessEnvironmentSource> environmentSource =
            CreateEnvironmentSource();

        Mock<IOsUtilityService> osUtilityService =
            CreateOsUtilityService();

        var explicitVariables =
            new Dictionary<string, string?>
            {
                ["BAD=NAME"] = "value"
            };

        var sut =
            new ChildEnvironmentResolver(
                environmentSource.Object,
                osUtilityService.Object);

        Action act =
            () => sut.Resolve(
                ChildEnvironmentPolicies.Compatible,
                explicitVariables);

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage(
                "*Environment variable names cannot contain '='.*");
    }

    [Fact]
    public void Resolve_WhenExplicitVariableNameContainsNullCharacter_ShouldThrow()
    {
        Mock<IProcessEnvironmentSource> environmentSource =
            CreateEnvironmentSource();

        Mock<IOsUtilityService> osUtilityService =
            CreateOsUtilityService();

        var explicitVariables =
            new Dictionary<string, string?>
            {
                ["BAD\0NAME"] = "value"
            };

        var sut =
            new ChildEnvironmentResolver(
                environmentSource.Object,
                osUtilityService.Object);

        Action act =
            () => sut.Resolve(
                ChildEnvironmentPolicies.Compatible,
                explicitVariables);

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage(
                "*Environment variable names cannot contain null characters.*");
    }

    [Fact]
    public void Resolve_WhenExplicitVariableValueContainsNullCharacter_ShouldThrow()
    {
        Mock<IProcessEnvironmentSource> environmentSource =
            CreateEnvironmentSource();

        Mock<IOsUtilityService> osUtilityService =
            CreateOsUtilityService();

        var explicitVariables =
            new Dictionary<string, string?>
            {
                ["TOKEN"] = "abc\0def"
            };

        var sut =
            new ChildEnvironmentResolver(
                environmentSource.Object,
                osUtilityService.Object);

        Action act =
            () => sut.Resolve(
                ChildEnvironmentPolicies.Compatible,
                explicitVariables);

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage(
                "*Environment variable 'TOKEN' cannot contain null characters in its value.*");
    }

    [Fact]
    public void Resolve_WhenOverrideValueContainsNullCharacter_ShouldThrow()
    {
        Mock<IProcessEnvironmentSource> environmentSource =
            CreateEnvironmentSource();

        Mock<IOsUtilityService> osUtilityService =
            CreateOsUtilityService();

        var policy =
            new ChildEnvironmentPolicy
            {
                Mode = ChildEnvironmentMode.InheritAll,
                Overrides =
                    new Dictionary<string, string?>
                    {
                        ["TOKEN"] = "abc\0def"
                    }
            };

        var sut =
            new ChildEnvironmentResolver(
                environmentSource.Object,
                osUtilityService.Object);

        Action act =
            () => sut.Resolve(
                policy,
                new Dictionary<string, string?>());

        act.Should()
            .Throw<ArgumentException>()
            .WithMessage(
                "*Environment variable 'TOKEN' cannot contain null characters in its value.*");
    }

    [Fact]
    public void Resolve_WhenExplicitVariableValueIsNull_ShouldAllowRemovalMutation()
    {
        Mock<IProcessEnvironmentSource> environmentSource =
            CreateEnvironmentSource();

        Mock<IOsUtilityService> osUtilityService =
            CreateOsUtilityService();

        var explicitVariables =
            new Dictionary<string, string?>
            {
                ["TOKEN"] = null
            };

        var sut =
            new ChildEnvironmentResolver(
                environmentSource.Object,
                osUtilityService.Object);

        IReadOnlyDictionary<string, string?> result =
            sut.Resolve(
                ChildEnvironmentPolicies.Compatible,
                explicitVariables);

        result.Should()
            .ContainKey("TOKEN");

        result["TOKEN"].Should().BeNull();
    }

    private static Mock<IProcessEnvironmentSource>
        CreateEnvironmentSource()
    {
        var environmentSource =
            new Mock<IProcessEnvironmentSource>(
                MockBehavior.Strict);

        environmentSource
            .Setup(source =>
                source.GetEnvironmentVariables())
            .Returns(
                new Dictionary<string, string?>(
                    StringComparer.Ordinal));

        return environmentSource;
    }

    private static Mock<IOsUtilityService>
        CreateOsUtilityService()
    {
        var osUtilityService =
            new Mock<IOsUtilityService>(
                MockBehavior.Strict);

        osUtilityService
            .Setup(service =>
                service.GetComparer())
            .Returns(
                StringComparer.Ordinal);

        return osUtilityService;
    }
}