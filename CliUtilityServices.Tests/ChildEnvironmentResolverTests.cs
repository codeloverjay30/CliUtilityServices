using CliUtilityServices.Security;
using EnvironmentUtilityServices;
using FluentAssertions;
using Moq;

namespace CliUtilityServices.Security.Tests;

public sealed partial class ChildEnvironmentResolverTests
{
    private readonly Mock<IOsUtilityService> _mockOsUtilityService = new(MockBehavior.Strict); 
    
    public ChildEnvironmentResolverTests()
    {
        _mockOsUtilityService.Setup(osUtil => osUtil.GetComparison()).Returns(StringComparison.OrdinalIgnoreCase);
        _mockOsUtilityService.Setup(osUtil => osUtil.GetComparer()).Returns(StringComparer.OrdinalIgnoreCase);
        _mockOsUtilityService.Setup(osUtil => osUtil.NormalizePath(It.IsAny<string>())).Returns(@"C:\");
    }

    [Fact]
    public void Resolve_WhenPolicyIsIsolated_ShouldRemoveAllInheritedVariables()
    {
        var environmentSource =
            new Mock<IProcessEnvironmentSource>(
                MockBehavior.Strict);

        environmentSource
            .Setup(x => x.GetEnvironmentVariables())
            .Returns(
                new Dictionary<string, string?>
                {
                    ["PATH"] = "/usr/bin",
                    ["SECRET"] = "sensitive",
                    ["TOKEN"] = "token-value"
                });

        var sut =
            new ChildEnvironmentResolver(
                environmentSource.Object,
                _mockOsUtilityService.Object
            );

        IReadOnlyDictionary<string, string?> actual =
            sut.Resolve(
                ChildEnvironmentPolicies.Isolated,
                new Dictionary<string, string?>
                {
                    ["SAFE_VALUE"] = "123"
                });

        actual["PATH"].Should().BeNull();
        actual["SECRET"].Should().BeNull();
        actual["TOKEN"].Should().BeNull();

        actual["SAFE_VALUE"].Should().Be("123");
    }

    [Fact]
    public void Resolve_WhenVariableIsDenied_ShouldNotAllowExplicitOverrideToRestoreIt()
    {
        var environmentSource =
            new Mock<IProcessEnvironmentSource>(
                MockBehavior.Strict);

        environmentSource
            .Setup(x => x.GetEnvironmentVariables())
            .Returns(
                new Dictionary<string, string?>
                {
                    ["SECRET"] = "parent-secret"
                });

        var policy = new ChildEnvironmentPolicy
        {
            Mode = ChildEnvironmentMode.InheritAll,
            DeniedVariables =
                new HashSet<string>
                {
                    "SECRET"
                }
        };

        var sut =
            new ChildEnvironmentResolver(
                environmentSource.Object,
                _mockOsUtilityService.Object
            );

        IReadOnlyDictionary<string, string?> actual =
            sut.Resolve(
                policy,
                new Dictionary<string, string?>
                {
                    ["SECRET"] = "attempted-override"
                });

        actual["SECRET"].Should().BeNull();
    }
}