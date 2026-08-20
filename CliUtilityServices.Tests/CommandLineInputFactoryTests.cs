using System;
using System.IO.Abstractions;
using System.Text;
using CliUtilityServices;
using FluentAssertions;
using Moq;
using EnvironmentUtilityServices;
using Xunit;

namespace CliUtilityServices.Tests
{
    /// <summary>
    /// Provides defensive unit tests for <see cref="CommandLineInputFactory"/> ensuring cross-platform predictability.
    /// </summary>
    public class CommandLineInputFactoryTests
    {
        private readonly Mock<IFileSystem> _mockFileSystem;
        private readonly Mock<IEnvironmentService> _mockEnvironmentService;
        private readonly Mock<IPath> _mockPath;

        public CommandLineInputFactoryTests()
        {
            // 防禦性設定：註冊編碼表，防止特定平台缺乏 Big5 導致初始化失敗
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            _mockFileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
            _mockEnvironmentService = new Mock<IEnvironmentService>(MockBehavior.Strict);
            _mockPath = new Mock<IPath>(MockBehavior.Strict);

            // 預防 Mock 導覽屬性引發的 NullReferenceException 副作用
            _mockFileSystem.Setup(f => f.Path).Returns(_mockPath.Object);
        }

        #region CreateShellInput Defensive Tests

        [Fact]
        public void CreateShellInput_WhenWindowsPlatform_ShouldReturnCmdInputWithBig5()
        {
            // Arrange
            _mockEnvironmentService.Setup(e => e.IsWindows()).Returns(true);
            _mockPath.Setup(p => p.Combine(It.IsAny<string>(), "cmd.exe"))
                     .Returns(@"C:\Windows\System32\cmd.exe");

            var factory = new CommandLineInputFactory(_mockFileSystem.Object, _mockEnvironmentService.Object);
            var arguments = "echo hello";

            // Act
            var result = factory.CreateShellInput(arguments);

            // Assert (使用 FluentAssertions 嚴格驗證)
            result.Should().NotBeNull();
            result.Command.Should().Be(@"C:\Windows\System32\cmd.exe");
            result.Arguments.Should().ContainInOrder("/c", "echo hello");
            result.InputEncoding.Should().Be(Encoding.GetEncoding("Big5"));
            result.OutputEncoding.Should().Be(Encoding.GetEncoding("Big5"));
            
            _mockEnvironmentService.Verify(e => e.IsWindows(), Times.Once);
        }

        [Fact]
        public void CreateShellInput_WhenNonWindowsPlatform_ShouldReturnBashInputWithUtf8()
        {
            // Arrange
            _mockEnvironmentService.Setup(e => e.IsWindows()).Returns(false);

            var factory = new CommandLineInputFactory(_mockFileSystem.Object, _mockEnvironmentService.Object);
            var arguments = "ls -la";

            // Act
            var result = factory.CreateShellInput(arguments);

            // Assert
            result.Should().NotBeNull();
            result.Command.Should().Be("bash");
            result.Arguments.Should().ContainInOrder("-c", "ls -la");
            result.InputEncoding.Should().Be(Encoding.UTF8);
            result.OutputEncoding.Should().Be(Encoding.UTF8);
        }

        [Fact]
        public void CreateShellInput_WithCustomWindowsCmdPath_ShouldPrioritizeCustomPath()
        {
            // Arrange
            _mockEnvironmentService.Setup(e => e.IsWindows()).Returns(true);
            var customPath = @"D:\Custom\cmd.exe";
            
            var factory = new CommandLineInputFactory(
                _mockFileSystem.Object, 
                _mockEnvironmentService.Object, 
                customWindowsCmdPath: customPath);

            // Act
            var result = factory.CreateShellInput("dir", @"C:\Temp");

            // Assert
            result.Command.Should().Be(customPath);
            result.WorkingDirectory.Should().Be(@"C:\Temp");
        }

        #endregion

        #region CreateWindowsBashInput Defensive Tests

        [Fact]
        public void CreateWindowsBashInput_WhenNonWindowsPlatform_ShouldThrowPlatformNotSupportedException()
        {
            // Arrange
            _mockEnvironmentService.Setup(e => e.IsWindows()).Returns(false);
            var factory = new CommandLineInputFactory(_mockFileSystem.Object, _mockEnvironmentService.Object);

            // Act
            Action act = () => factory.CreateWindowsBashInput("echo 123");

            // Assert (鐵律：嚴格驗證異常型態與關鍵字訊息)
            act.Should().Throw<PlatformNotSupportedException>()
               .WithMessage("*only supports on Windows*");
        }

        [Fact]
        public void CreateWindowsBashInput_WhenWindowsPlatform_ShouldReturnGitBashInputWithUtf8()
        {
            // Arrange
            _mockEnvironmentService.Setup(e => e.IsWindows()).Returns(true);
            var factory = new CommandLineInputFactory(_mockFileSystem.Object, _mockEnvironmentService.Object);

            // Act
            var result = factory.CreateWindowsBashInput("echo 'hello'");

            // Assert
            result.Command.Should().Be(@"C:\Program Files\Git\bin\bash.exe");
            result.Arguments.Should().ContainInOrder("-c", "echo 'hello'");
            result.InputEncoding.Should().Be(Encoding.UTF8);
            result.OutputEncoding.Should().Be(Encoding.UTF8);
        }

        #endregion
    }
}