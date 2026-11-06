using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.Reports.Services;

/// <summary>
/// Renders typed report values as HTML in the report views.
/// </summary>
public static class ReportValueHtml
{
    /// <summary>
    /// Renders a link value as an anchor, with its text and its address encoded.
    /// </summary>
    /// <param name="value">The report value, as made by <see cref="ReportValue.Link(string, string)"/>.</param>
    /// <param name="html">The anchor when successful.</param>
    /// <returns><see langword="true"/> when the value is a link.</returns>
    public static bool TryCreateLink(string value, out IHtmlContent html)
    {
        html = null;

        if (!ReportValue.TryGetLink(value, out var text, out var url))
        {
            return false;
        }

        var anchor = new TagBuilder("a");
        anchor.Attributes["href"] = url;
        anchor.InnerHtml.Append(text);
        html = anchor;

        return true;
    }
}
