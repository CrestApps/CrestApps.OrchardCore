using System.Text;

namespace CrestApps.OrchardCore.Reports;

/// <summary>
/// Creates typed report values that the shared report renderer resolves before display or export.
/// </summary>
public static class ReportValue
{
    private const string UserDisplayNamePrefix = "\u001Euser-display-name:";
    private const string ValueSuffix = "\u001F";

    /// <summary>
    /// Creates a cached user display-name value from a stored username.
    /// </summary>
    /// <param name="userName">The username used to resolve the user display name.</param>
    /// <param name="fallback">The text returned when no username is available.</param>
    /// <returns>A report value that resolves through the user display-name shape.</returns>
    public static string UserDisplayName(string userName, string fallback)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return fallback;
        }

        return UserDisplayNamePrefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(userName)) + ValueSuffix;
    }

    /// <summary>
    /// Attempts to read a username from a typed user display-name value.
    /// </summary>
    /// <param name="value">The report value.</param>
    /// <param name="userName">The decoded username when successful.</param>
    /// <returns><see langword="true"/> when the value contains a user display-name reference.</returns>
    public static bool TryGetUserName(string value, out string userName)
    {
        userName = null;

        if (string.IsNullOrEmpty(value) ||
            !value.StartsWith(UserDisplayNamePrefix, StringComparison.Ordinal) ||
            !value.EndsWith(ValueSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        var encodedValue = value.Substring(
            UserDisplayNamePrefix.Length,
            value.Length - UserDisplayNamePrefix.Length - ValueSuffix.Length);

        try
        {
            userName = Encoding.UTF8.GetString(Convert.FromBase64String(encodedValue));

            return !string.IsNullOrWhiteSpace(userName);
        }
        catch (FormatException)
        {
            userName = null;

            return false;
        }
    }
}
