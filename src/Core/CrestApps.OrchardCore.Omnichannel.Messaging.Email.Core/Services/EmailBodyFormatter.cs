using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;

/// <summary>
/// Turns the plain text an agent or the AI wrote into the two bodies of an email: the text as written, and a simple
/// HTML version with its paragraphs, line breaks and links kept. The signature and an unsubscribe link go below both.
/// </summary>
public static partial class EmailBodyFormatter
{
    /// <summary>
    /// Builds the plain-text body.
    /// </summary>
    /// <param name="body">The message as written.</param>
    /// <param name="signature">The address's signature, if any.</param>
    /// <param name="unsubscribeUrl">The unsubscribe link, when the email carries one.</param>
    /// <param name="unsubscribeText">The words shown before the unsubscribe link.</param>
    /// <returns>The plain-text body.</returns>
    public static string ToText(string body, string signature, string unsubscribeUrl, string unsubscribeText)
    {
        var builder = new StringBuilder(Normalize(body));

        var normalizedSignature = Normalize(signature);

        if (!string.IsNullOrEmpty(normalizedSignature))
        {
            // "-- " on its own line is the signature separator mail clients recognise and fold.
            builder.Append("\n\n-- \n").Append(normalizedSignature);
        }

        if (!string.IsNullOrEmpty(unsubscribeUrl))
        {
            builder.Append("\n\n").Append(unsubscribeText).Append(' ').Append(unsubscribeUrl);
        }

        return builder.ToString().Replace("\n", "\r\n", StringComparison.Ordinal);
    }

    /// <summary>
    /// Builds the HTML body.
    /// </summary>
    /// <param name="body">The message as written.</param>
    /// <param name="signature">The address's signature, if any.</param>
    /// <param name="unsubscribeUrl">The unsubscribe link, when the email carries one.</param>
    /// <param name="unsubscribeText">The words shown as the unsubscribe link.</param>
    /// <returns>The HTML body.</returns>
    public static string ToHtml(string body, string signature, string unsubscribeUrl, string unsubscribeText)
    {
        var builder = new StringBuilder();

        builder.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"></head>");
        builder.Append("<body style=\"font-family: -apple-system, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; font-size: 14px; line-height: 1.5; color: #1f2328;\">");

        AppendParagraphs(builder, Normalize(body));

        var normalizedSignature = Normalize(signature);

        if (!string.IsNullOrEmpty(normalizedSignature))
        {
            builder.Append("<div style=\"margin-top: 16px; color: #59636e;\">");
            AppendParagraphs(builder, normalizedSignature);
            builder.Append("</div>");
        }

        if (!string.IsNullOrEmpty(unsubscribeUrl))
        {
            builder.Append("<p style=\"margin-top: 24px; font-size: 12px; color: #818b98;\"><a href=\"")
                .Append(WebUtility.HtmlEncode(unsubscribeUrl))
                .Append("\" style=\"color: #818b98;\">")
                .Append(WebUtility.HtmlEncode(unsubscribeText))
                .Append("</a></p>");
        }

        builder.Append("</body></html>");

        return builder.ToString();
    }

    private static void AppendParagraphs(StringBuilder builder, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        foreach (var paragraph in BlankLines().Split(text))
        {
            if (string.IsNullOrWhiteSpace(paragraph))
            {
                continue;
            }

            builder.Append("<p style=\"margin: 0 0 12px 0;\">");

            var lines = paragraph.Split('\n');

            for (var index = 0; index < lines.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append("<br>");
                }

                AppendLine(builder, lines[index]);
            }

            builder.Append("</p>");
        }
    }

    // Links are made clickable; everything else is encoded, so nothing the writer typed is ever taken as markup.
    private static void AppendLine(StringBuilder builder, string line)
    {
        var position = 0;

        foreach (Match match in Links().Matches(line))
        {
            builder.Append(WebUtility.HtmlEncode(line.Substring(position, match.Index - position)));

            var url = match.Value.TrimEnd('.', ',', ')', ';', ':', '!', '?');
            var encoded = WebUtility.HtmlEncode(url);

            builder.Append("<a href=\"").Append(encoded).Append("\">").Append(encoded).Append("</a>");
            builder.Append(WebUtility.HtmlEncode(match.Value.Substring(url.Length)));

            position = match.Index + match.Length;
        }

        builder.Append(WebUtility.HtmlEncode(line.Substring(position)));
    }

    private static string Normalize(string text)
        => string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Trim();

    [GeneratedRegex(@"\n\s*\n")]
    private static partial Regex BlankLines();

    [GeneratedRegex(@"https?://[^\s<>""']+", RegexOptions.IgnoreCase)]
    private static partial Regex Links();
}
