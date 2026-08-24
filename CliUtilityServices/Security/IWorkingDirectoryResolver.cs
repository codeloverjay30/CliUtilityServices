namespace CliUtilityServices.Security;

/// <summary>
/// Defines a contract for resolving and validating child-process working directories.
/// </summary>
public interface IWorkingDirectoryResolver
{
    /// <summary>
    /// Resolves and validates the requested child-process working directory.
    /// </summary>
    /// <param name="workingDirectory">
    /// The requested working directory.
    /// </param>
    /// <returns>
    /// The validated canonical absolute working directory.
    /// </returns>
    string Resolve(string workingDirectory);
}