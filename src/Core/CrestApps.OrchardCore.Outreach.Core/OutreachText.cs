using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Outreach.Core;

/// <summary>
/// Text helpers for outreach messages.
/// </summary>
public static partial class OutreachText
{
    /// <summary>
    /// Returns the plain-text version of an HTML message: block ends and line breaks become new lines, tags are dropped
    /// and entities decoded.
    /// </summary>
    /// <param name="html">The HTML.</param>
    /// <returns>The text.</returns>
    public static string FromHtml(string html)
        => System.Net.WebUtility.HtmlDecode(Tags().Replace(LineBreaks().Replace(html ?? string.Empty, "\n"), string.Empty)).Trim();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex("<br\\s*/?>|</p>|</div>|</h[1-6]>|</li>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreaks();
}
