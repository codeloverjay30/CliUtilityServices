using CliUtilityServices.Security;
using EnvironmentUtilityServices;
using FluentAssertions;
using Moq;

namespace CliUtilityServices.Tests;

public sealed partial class ChildEnvironmentResolverTests
{
    [Fact]
    public void Resolve_WhenWindowsAllowListUsesDifferentCasing_ShouldAllowExplicitVariable()
    {
        var environmentSource =
            new Mock<IProcessEnvironmentSource>(
                MockBehavior.Strict);

        var osUtilityService =
            new Mock<IOsUtilityService>(
                MockBehavior.Strict);

        environmentSource
            .Setup(source =>
                source.GetEnvironmentVariables())
            .Returns(
                new Dictionary<string, string?>(
                    StringComparer.Ordinal)
                {
                    ["PATH"] = @"C:\Windows"
                });

        osUtilityService
            .Setup(service =>
                service.GetComparer())
            .Returns(
                StringComparer.OrdinalIgnoreCase);

        var policy =
            new ChildEnvironmentPolicy
            {
                Mode =
                    ChildEnvironmentMode.AllowList,

                AllowedVariables =
                    new HashSet<string>(
                        StringComparer.Ordinal)
                    {
                    "PATH"
                    }
            };

        var explicitVariables =
            new Dictionary<string, string?>(
                StringComparer.Ordinal)
            {
                ["Path"] = @"C:\Tools"
            };

        var sut =
            new ChildEnvironmentResolver(
                environmentSource.Object,
                osUtilityService.Object);

        Action act =
            () => sut.Resolve(
                policy,
                explicitVariables);

        act.Should().NotThrow();
    }

    [Fact]
    public void Resolve_WhenWindowsDenyListUsesDifferentCasing_ShouldDenyVariable()
    {
        var environmentSource =
            new Mock<IProcessEnvironmentSource>(
                MockBehavior.Strict);

        var osUtilityService =
            new Mock<IOsUtilityService>(
                MockBehavior.Strict);

        environmentSource
            .Setup(source =>
                source.GetEnvironmentVariables())
            .Returns(
                new Dictionary<string, string?>(
                    StringComparer.Ordinal));

        osUtilityService
            .Setup(service =>
                service.GetComparer())
            .Returns(
                StringComparer.OrdinalIgnoreCase);

        var policy =
            new ChildEnvironmentPolicy
            {
                Mode =
                    ChildEnvironmentMode.DenyList,

                DeniedVariables =
                    new HashSet<string>(
                        StringComparer.Ordinal)
                    {
                    "PATH"
                    }
            };

        var explicitVariables =
            new Dictionary<string, string?>(
                StringComparer.Ordinal)
            {
                ["Path"] = @"C:\Tools"
            };

        var sut =
            new ChildEnvironmentResolver(
                environmentSource.Object,
                osUtilityService.Object);

        IReadOnlyDictionary<string, string?> result =
            sut.Resolve(
                policy,
                explicitVariables);

        result.Should()
            .ContainKey("PATH");

        result["PATH"].Should().BeNull();

        result.Should().HaveCount(1);
    }
    [Fact]
    public void Resolve_WhenWindowsExplicitVariablesContainCaseInsensitiveCollision_ShouldThrow()
    {
        var environmentSource =
            new Mock<IProcessEnvironmentSource>(
                MockBehavior.Strict);

        var osUtilityService =
            new Mock<IOsUtilityService>(
                MockBehavior.Strict);

        environmentSource
            .Setup(source =>
                source.GetEnvironmentVariables())
            .Returns(
                new Dictionary<string, string?>());

        osUtilityService
            .Setup(service =>
                service.GetComparer())
            .Returns(
                StringComparer.OrdinalIgnoreCase);

        var explicitVariables =
            new Dictionary<string, string?>(
                StringComparer.Ordinal)
            {
                ["PATH"] = @"C:\One",
                ["Path"] = @"C:\Two"
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
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*Path*conflicts with another variable*" +
                "operating-system comparison rules*");
    }

    [Fact]
    public void Resolve_WhenUnixVariablesDifferOnlyByCase_ShouldPreserveBothVariables()
    {
        var environmentSource =
            new Mock<IProcessEnvironmentSource>(
                MockBehavior.Strict);

        var osUtilityService =
            new Mock<IOsUtilityService>(
                MockBehavior.Strict);

        environmentSource
            .Setup(source =>
                source.GetEnvironmentVariables())
            .Returns(
                new Dictionary<string, string?>());

        osUtilityService
            .Setup(service =>
                service.GetComparer())
            .Returns(
                StringComparer.Ordinal);

        var explicitVariables =
            new Dictionary<string, string?>(
                StringComparer.Ordinal)
            {
                ["PATH"] = "/usr/bin",
                ["Path"] = "/custom/bin"
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
            .Contain(
                new KeyValuePair<string, string?>(
                    "PATH",
                    "/usr/bin"));

        result.Should()
            .Contain(
                new KeyValuePair<string, string?>(
                    "Path",
                    "/custom/bin"));

        result.Should().HaveCount(2);
    }
}
