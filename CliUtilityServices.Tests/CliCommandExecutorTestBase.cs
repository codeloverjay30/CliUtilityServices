using System.IO.Abstractions;
using CommandResult.Infrastructure;
using EnvironmentUtilityServices;
using Moq;
using OsVersionUtilityServices;

namespace CliUtilityServices.Tests;

public abstract class CliCommandExecutorTestBase
{
    protected Mock<IFileSystem> FileSystem { get; }
    protected Mock<IEnvironmentService> EnvironmentService { get; }
    protected Mock<IOSVersionResolver> OsVersionResolver { get; }
    protected Mock<ICommandExecutionEngine> ExecutionEngine { get; }

    protected CliCommandExecutor Sut { get; }

    protected CliCommandExecutorTestBase()
    {
        FileSystem = new Mock<IFileSystem>(MockBehavior.Strict);

        EnvironmentService =
            new Mock<IEnvironmentService>(MockBehavior.Strict);

        OsVersionResolver =
            new Mock<IOSVersionResolver>(MockBehavior.Strict);

        ExecutionEngine =
            new Mock<ICommandExecutionEngine>(MockBehavior.Strict);

        Sut = new CliCommandExecutor(
            FileSystem.Object,
            EnvironmentService.Object,
            OsVersionResolver.Object,
            ExecutionEngine.Object);
    }
}