using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Validates the tenant-configured PJSIP Realtime table prefix as a strict SQL identifier fragment.
/// The prefix is concatenated directly into SQL table names (for example <c>ps_auths</c>), so it must
/// never contain characters that could break out of the identifier and inject SQL or target another
/// tenant's tables in a shared PJSIP Realtime database.
/// </summary>
internal static partial class AsteriskPjsipRealtimeTablePrefixValidator
{
    [GeneratedRegex("^[A-Za-z0-9_]+(\\.[A-Za-z0-9_]*)?$", RegexOptions.CultureInvariant)]
    private static partial Regex PrefixRegex();

    /// <summary>
    /// Determines whether the supplied table prefix is a safe SQL identifier fragment. An empty or
    /// whitespace-only prefix is treated as valid because it means "no prefix". A non-empty prefix is
    /// valid only when it consists of one identifier segment, optionally preceded by a single
    /// <c>schema.</c> qualifier, where every segment contains only ASCII letters, digits, or underscores.
    /// </summary>
    /// <param name="prefix">The configured table prefix to validate.</param>
    /// <returns><see langword="true"/> when the prefix is safe to concatenate into a SQL table name; otherwise, <see langword="false"/>.</returns>
    public static bool IsValid(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return true;
        }

        return PrefixRegex().IsMatch(prefix);
    }

    /// <summary>
    /// Ensures the supplied table prefix is a safe SQL identifier fragment, throwing when it is not.
    /// This is the defense-in-depth boundary applied at the SQL layer so an unvalidated prefix can never
    /// be concatenated into a command.
    /// </summary>
    /// <param name="prefix">The configured table prefix to validate.</param>
    /// <returns>The trimmed prefix, or an empty string when no prefix is configured.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the prefix is not a valid SQL identifier fragment.</exception>
    public static string EnsureValid(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return string.Empty;
        }

        var trimmed = prefix.Trim();

        if (!PrefixRegex().IsMatch(trimmed))
        {
            throw new InvalidOperationException("The configured PJSIP Realtime table prefix is not a valid SQL identifier.");
        }

        return trimmed;
    }
}
