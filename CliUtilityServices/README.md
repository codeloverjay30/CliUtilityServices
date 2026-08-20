# Description
Utility service to more easily execute CLI command through `CliWrap` package.

# Features
## 1.0.0-preview-1.0.0
### Added
+ execute CLI command

## 1.1.0-preview-1.0.0
### Added
+ Add a factory class. 

According to OS type on runtime, to specify the Arguments and executing terminal file path.

## 1.2.0-preview-1.0.0
### Added
+ `Memberwise` Clone the POCO

## 2.0.0-preview-1.0.0
### Major Updates
+ Use record class so that it is more easily to use `with` preserved word to copy the instance.

## 2.1.0-preview-1.0.0
### Bug fix
+ Solve `Big5` etc encoding name is not supported on codespace.

## 2.2.0-preview-1.0.0
### Bug fix
+ Always ensure `Big5` etc encodings are registered.

## 2.3.0-preview-1.0.0
### Added
+ Can configure Validation (`CommandResultValidation` class) used when non-zero exit code are returned

## 3.0.0-preview-1.0.0
### Potential security bug fix
+ Avoid command injection attack and command injection attack (see below `Chnaged API` section)

### Major Updates
+ Make it more maintenable (for me),

since I refactored it with lots of `Strategy Pattern` instead of `Factory Pattern` 

(e.g. refactor `CommandLineInputFactory` with `ITerminalProvider` and its implemented class)

### Changed API
+ For easily generating command input (see above `Major Updates` section)

`CommandLineInputFactory` => `ITerminalProvider` and its implemented class

+ For security (avoiding command injection attack and command injection attack),

```
global::CliUtilityServices.CliWrapRunner.ExecuteAsync(CommandLineInput commandLineInput)
```

=> 

```
global::CliUtilityServices.CliWrapRunner.ExecuteInShellAsync(
        TerminalTypeOptions terminalType,
        CommandLineInput commandLineInput
    )
```

## 4.0.0-preview-1.0.0
### Potential bug fix
+ Avoiding crash due to log many outputs.

Before:

- Directly log many outputs to terminal

After fixed:

You can choose one of approaches using different strategy pattern.

- log many outputs to specify file

- or log the last `n` lines of output to terminal.

## 4.1.0-preview-1.0.0
### Added
+ Use terminal according to the OS used (auto-detected) on runtime.

## 5.0.0-preview-1.0.0
### Added
+ Zsh terminal

+ An fluent API to create a command line input

### Major Updates
+ Refactor

### Refactor
+ Extract the output after execution with `Cli.Wrap` packages

    From `BufferedCommandResult` to my API `CommandExecutionResult`

+ Use an fluent API (new added) to create a command line input instead of factory.

