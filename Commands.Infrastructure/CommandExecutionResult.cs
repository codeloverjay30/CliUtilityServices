namespace Commands.Infrastructure;

public record CommandExecutionResult(
    string StandardOutput,
    string StandardError,
    int ExitCode,
    TimeSpan RunTime
);