// File: CliUtilityServices.Tests/FileStreamPipeStrategyTests.cs
using System;
using System.IO.Abstractions;
using System.Threading.Tasks;
using CliUtilityServices.Pipes;
using FluentAssertions;
using Moq;
using Xunit;

namespace CliUtilityServices.Tests;

/// <summary>
/// Provides defensive unit tests for revised <see cref="FileStreamPipeStrategy"/> using FluentAssertions.
/// </summary>
public class FileStreamPipeStrategyTests
{
    private readonly Mock<IFileSystem> _mockFileSystem;
    private readonly Mock<IPath> _mockPath;
    private readonly Mock<IFile> _mockFile;

    public FileStreamPipeStrategyTests()
    {
        _mockFileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        _mockPath = new Mock<IPath>(MockBehavior.Strict);
        _mockFile = new Mock<IFile>(MockBehavior.Strict);

        _mockFileSystem.Setup(f => f.Path).Returns(_mockPath.Object);
        _mockFileSystem.Setup(f => f.File).Returns(_mockFile.Object);
        
        _mockPath.Setup(p => p.GetTempPath()).Returns(@"C:\Temp\");
        _mockPath.Setup(p => p.Combine(It.IsAny<string>(), It.IsAny<string>()))
                 .Returns<string, string>((dir, file) => $"{dir}{file}");
    }

    [Fact]
    public async Task GetResultAsync_WhenStrategyIsDisposed_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var strategy = new FileStreamPipeStrategy(_mockFileSystem.Object);
        _mockFile.Setup(f => f.Exists(It.IsAny<string>())).Returns(false);

        // Act & Assert 處置
        await strategy.DisposeAsync();

        Func<Task> act = async () => await strategy.GetResultAsync();

        // Assert (FluentAssertions 鐵律驗證)
        await act.Should().ThrowAsync<ObjectDisposedException>()
                 .WithMessage("*FileStreamPipeStrategy*");
    }

    [Fact]
    public void Constructor_WhenFileSystemIsNull_ShouldThrowArgumentNullException()
    {
        // Act
        Action act = () => new FileStreamPipeStrategy(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
           .Which.ParamName.Should().Be("fileSystem");
    }
}