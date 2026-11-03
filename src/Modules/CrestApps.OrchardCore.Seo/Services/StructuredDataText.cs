using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Seo.Services;

/// <summary>
/// Turns HTML into the short plain text that schema.org text properties, such as <c>description</c>, hold.
/// </summary>
internal static partial class StructuredDataText
{
    /// <summary>
    /// Removes the markup of an HTML fragment, decodes its entities and collapses its white space.
    /// </summary>
    /// <param name="html">The HTML.</param>
    /// <returns>The plain text.</returns>
    public static string ToPlainText(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var withoutScripts = ScriptOrStyleRegex().Replace(html, " ");
        var withoutTags = TagRegex().Replace(withoutScripts, " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);

        return WhiteSpaceRegex().Replace(decoded, " ").Trim();
    }

    /// <summary>
    /// Shortens text to a maximum length, at a word boundary when one is close, and ends it with an ellipsis.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="maxLength">The maximum length.</param>
    /// <returns>The text, shortened when it is longer than <paramref name="maxLength"/>.</returns>
    public static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text;
        }

        var cut = text.LastIndexOf(' ', maxLength - 1);

        if (cut < maxLength / 2)
        {
            cut = maxLength - 1;
        }

        var builder = new StringBuilder(text, 0, cut, cut + 1);
        builder.Append('…');

        return builder.ToString().TrimEnd();
    }

    [GeneratedRegex(@"<(script|style)\b[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptOrStyleRegex();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpaceRegex();
}
