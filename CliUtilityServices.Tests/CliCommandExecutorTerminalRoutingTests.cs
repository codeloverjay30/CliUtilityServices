using System.IO.Abstractions;
using System.Text;
using CliUtilityServices.Security;
using CommandResult.Infrastructure;
using Commands.Infrastructure;
using EnvironmentUtilityServices;
using FluentAssertions;
using Moq;
using OsVersionUtilityServices;

namespace CliUtilityServices.Tests;

public partial class CliCommandExecutorTerminalRoutingTests
{
    private readonly Mock<IFile> _mockFile = new(MockBehavior.Strict);
    private readonly Mock<IFileSystem> _mockFileSystem = new(MockBehavior.Strict);
    private readonly Mock<IPath> _mockPath;
    private readonly Mock<IEnvironmentService> _mockEnvironmentService = new(MockBehavior.Strict);
    private readonly Mock<IOSVersionResolver> _mockOsVersionResolver = new(MockBehavior.Strict);
    private readonly Mock<ICliResultProcessor> _mockResultProcessor = new(MockBehavior.Strict);
    private readonly Mock<ICommandExecutionEngine> _mockExecutionEngine = new(MockBehavior.Strict);

    private readonly Mock<IExecutableResolver> _mockTrustedExecutableRegistry = new(MockBehavior.Strict);

    private readonly Mock<IChildEnvironmentResolver> _mockChildEnvironmentResolver = new(MockBehavior.Strict);
    private readonly CliCommandExecutor _sut;

    public CliCommandExecutorTerminalRoutingTests()
    {
        Encoding.RegisterProvider(
            CodePagesEncodingProvider.Instance);

        _mockFileSystem =
            new Mock<IFileSystem>(MockBehavior.Strict);

        _mockFile =
            new Mock<IFile>(MockBehavior.Strict);

        _mockPath =
            new Mock<IPath>(MockBehavior.Strict);

        _mockEnvironmentService =
            new Mock<IEnvironmentService>(MockBehavior.Strict);

        _mockOsVersionResolver =
            new Mock<IOSVersionResolver>(MockBehavior.Strict);

        _mockExecutionEngine =
            new Mock<ICommandExecutionEngine>(MockBehavior.Strict);


        _mockFile.Setup(file => file.Exists(It.IsAny<string>())).Returns(true);
        _mockPath.Setup(path => path.Combine(It.IsAny<string>(), It.IsAny<string>())).Returns(@"C:\WINDOWS\system32\cmd.exe");
        
        _mockFileSystem
            .SetupGet(x => x.File)
            .Returns(_mockFile.Object);

        _mockFileSystem
            .SetupGet(x => x.Path)
            .Returns(_mockPath.Object);

        _mockEnvironmentService.Setup(env => env.IsWindows()).Returns(true);
        _mockEnvironmentService.Setup(env => env.IsLinux()).Returns(false);
        _mockEnvironmentService.Setup(env => env.IsMacOS()).Returns(false);
        _mockEnvironmentService.Setup(env => env.IsUncPath(It.IsAny<string>())).Returns(true);

        _sut =
            new CliCommandExecutor(
                _mockFileSystem.Object,
                _mockEnvironmentService.Object,
                _mockOsVersionResolver.Object,
                _mockExecutionEngine.Object);
    }

    [Theory]
    [InlineData(TerminalTypeOptions.Cmd)]
    [InlineData(TerminalTypeOptions.PowerShell)]
    [InlineData(TerminalTypeOptions.PowerShellCore)]
    [InlineData(TerminalTypeOptions.Bash)]
    [InlineData(TerminalTypeOptions.Zsh)]
    public async Task ExecuteTrustedScriptAsync_WhenTerminalTypeIsSupported_ShouldResolveProvider(
    TerminalTypeOptions terminalType)
    {
        // Arrange
        const string trustedScript = "echo test";

        // 每個 provider 所需的 OS/FileSystem mock
        // 應依 terminalType 分開 Setup

        _mockExecutionEngine
            .Setup(x => x.ExecuteAsync(
                It.IsAny<CommandLineInput>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new CommandExecutionResult(
                    string.Empty,
                    string.Empty,
                    0,
                    TimeSpan.Zero));

        // Act
        Func<Task> act = async () =>
            await _sut.ExecuteTrustedScriptAsync(
                terminalType,
                trustedScript);

        // Assert
        await act.Should()
            .NotThrowAsync();
    }

}
