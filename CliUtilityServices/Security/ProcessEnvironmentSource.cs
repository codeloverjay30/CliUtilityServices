using System.Collections;

namespace CliUtilityServices.Security;

/// <summary>
/// Provides environment variables from the current operating-system process.
/// </summary>
public sealed class ProcessEnvironmentSource : IProcessEnvironmentSource
{
    /// <inheritdoc />
    public IReadOnlyDictionary<string, string?> GetEnvironmentVariables()
    {
        StringComparer comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

        var result =
            new Dictionary<string, string?>(comparer);

        IDictionary variables =
            Environment.GetEnvironmentVariables();

        foreach (DictionaryEntry entry in variables)
        {
            if (entry.Key is string name)
            {
                result[name] = entry.Value?.ToString();
            }
        }

        return result;
    }
}