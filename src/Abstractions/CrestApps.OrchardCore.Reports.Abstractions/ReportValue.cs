using System.Text;

namespace CrestApps.OrchardCore.Reports;

/// <summary>
/// Creates typed report values that the shared report renderer resolves before display or export: a user's display
/// name, and a link that the browser shows as an anchor and an export writes as its text.
/// </summary>
public static class ReportValue
{
    private const string UserDisplayNamePrefix = "\u001Euser-display-name:";
    private const string LinkPrefix = "\u001Elink:";
    private const char LinkSeparator = ':';
    private const string ValueSuffix = "\u001F";

    /// <summary>
    /// Creates a value shown as a link in the browser and as its text in an export, so any report can let a person
    /// open what a cell names, such as a conversation or an order.
    /// </summary>
    /// <param name="text">The text of the cell, which is also what an export writes.</param>
    /// <param name="url">The address the link opens: a path on the site that starts with a single <c>/</c>, or an
    /// absolute <c>http</c> or <c>https</c> address.</param>
    /// <returns>A report value that renders as a link, or <paramref name="text"/> alone when there is no text or the
    /// address is empty or not one of those kinds.</returns>
    public static string Link(string text, string url)
    {
        if (string.IsNullOrEmpty(text) || !IsSafeUrl(url))
        {
            return text;
        }

        return LinkPrefix +
            Convert.ToBase64String(Encoding.UTF8.GetBytes(url)) +
            LinkSeparator +
            Convert.ToBase64String(Encoding.UTF8.GetBytes(text)) +
            ValueSuffix;
    }

    /// <summary>
    /// Attempts to read the text and the address of a link value.
    /// </summary>
    /// <param name="value">The report value.</param>
    /// <param name="text">The text of the link when successful.</param>
    /// <param name="url">The address of the link when successful.</param>
    /// <returns><see langword="true"/> when the value is a link with text and an address that may be rendered.</returns>
    public static bool TryGetLink(string value, out string text, out string url)
    {
        text = null;
        url = null;

        if (string.IsNullOrEmpty(value) ||
            !value.StartsWith(LinkPrefix, StringComparison.Ordinal) ||
            !value.EndsWith(ValueSuffix, StringComparison.Ordinal))
        {
            return false;
        }

        var encodedValue = value.Substring(LinkPrefix.Length, value.Length - LinkPrefix.Length - ValueSuffix.Length);
        var separator = encodedValue.IndexOf(LinkSeparator, StringComparison.Ordinal);

        if (separator <= 0)
        {
            return false;
        }

        try
        {
            var decodedUrl = Encoding.UTF8.GetString(Convert.FromBase64String(encodedValue[..separator]));
            var decodedText = Encoding.UTF8.GetString(Convert.FromBase64String(encodedValue[(separator + 1)..]));

            if (string.IsNullOrEmpty(decodedText) || !IsSafeUrl(decodedUrl))
            {
                return false;
            }

            text = decodedText;
            url = decodedUrl;

            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the text a value shows: the text of a link, or the value itself. A user display-name value is returned
    /// unchanged, because only the display pipeline resolves it.
    /// </summary>
    /// <param name="value">The report value.</param>
    /// <returns>The text of the value.</returns>
    public static string GetText(string value)
        => TryGetLink(value, out var text, out _) ? text : value;

    private static bool IsSafeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        // A path on the site, but not "//host", which a browser reads as another site.
        if (url[0] == '/')
        {
            return url.Length == 1 || (url[1] != '/' && url[1] != '\\');
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

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
