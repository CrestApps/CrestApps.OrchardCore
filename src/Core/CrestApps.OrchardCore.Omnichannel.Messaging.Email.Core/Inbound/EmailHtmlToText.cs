using System.Net;
using System.Text.RegularExpressions;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Inbound;

/// <summary>
/// Turns an HTML email body into readable plain text for an email that came without a text part: block elements become
/// line breaks, quoted history is marked as quoted, links keep their address, and everything else is dropped. The
/// thread only ever shows plain text, so nothing the sender wrote is rendered as markup.
/// </summary>
public static partial class EmailHtmlToText
{
    /// <summary>
    /// Converts an HTML body to plain text.
    /// </summary>
    /// <param name="html">The HTML body.</param>
    /// <returns>The plain text, or an empty string when <paramref name="html"/> is empty.</returns>
    public static string Convert(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var text = html;

        // What is never shown: the head, scripts, styles and comments.
        text = Invisible().Replace(text, string.Empty);
        text = Comments().Replace(text, string.Empty);

        // A quoted reply (a blockquote, or the containers mail clients put the history in) is marked with "> " so the
        // reply parser recognises it as history, as it would in a text body.
        text = QuoteStart().Replace(text, "\n[[QUOTE]]");
        text = QuoteEnd().Replace(text, "[[/QUOTE]]\n");

        // A link keeps its address when the text shown is not the address itself.
        text = Links().Replace(text, match =>
        {
            var href = WebUtility.HtmlDecode(match.Groups["href"].Value).Trim();
            var label = Tags().Replace(match.Groups["label"].Value, string.Empty).Trim();

            return string.IsNullOrEmpty(href) || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) || WebUtility.HtmlDecode(label).Trim() == href
                ? label
                : $"{label} ({href})";
        });

        text = LineBreaks().Replace(text, "\n");
        text = BlockEnds().Replace(text, "\n");
        text = ListItems().Replace(text, "\n• ");
        text = Tags().Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text);

        text = text.Replace(' ', ' ');
        text = MarkQuotes(text);

        // Collapse the white space the markup left behind, keeping paragraphs apart.
        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => SpaceRuns().Replace(line, " ").TrimEnd());

        return ExcessBlankLines().Replace(string.Join('\n', lines), "\n\n").Trim();
    }

    // Prefixes every line between the quote markers with "> ", nesting as deep as the markup did.
    private static string MarkQuotes(string text)
    {
        if (!text.Contains("[[QUOTE]]", StringComparison.Ordinal))
        {
            return text;
        }

        var depth = 0;
        var output = new List<string>();

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine;
            var opens = CountOf(line, "[[QUOTE]]");
            var closes = CountOf(line, "[[/QUOTE]]");

            line = line.Replace("[[QUOTE]]", string.Empty, StringComparison.Ordinal).Replace("[[/QUOTE]]", string.Empty, StringComparison.Ordinal);
            depth += opens;

            output.Add(depth > 0 && !string.IsNullOrWhiteSpace(line)
                ? string.Concat(Enumerable.Repeat("> ", depth)) + line.Trim()
                : line);

            depth = Math.Max(0, depth - closes);
        }

        return string.Join('\n', output);
    }

    private static int CountOf(string text, string value)
    {
        var count = 0;
        var index = text.IndexOf(value, StringComparison.Ordinal);

        while (index >= 0)
        {
            count++;
            index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
        }

        return count;
    }

    [GeneratedRegex(@"<(head|script|style|title)\b[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Invisible();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex Comments();

    [GeneratedRegex(@"<(blockquote\b[^>]*|div\b[^>]*class\s*=\s*[""'][^""']*(gmail_quote|yahoo_quoted|moz-cite-prefix)[^""']*[""'][^>]*)>", RegexOptions.IgnoreCase)]
    private static partial Regex QuoteStart();

    [GeneratedRegex(@"</blockquote\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex QuoteEnd();

    [GeneratedRegex(@"<a\b[^>]*\bhref\s*=\s*[""'](?<href>[^""']*)[""'][^>]*>(?<label>.*?)</a\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Links();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreaks();

    [GeneratedRegex(@"</(p|div|tr|table|h[1-6]|ul|ol|li|section|article|header|footer)\s*>|<(p|div|tr|table|h[1-6]|hr)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockEnds();

    [GeneratedRegex(@"<li\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ListItems();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex Tags();

    [GeneratedRegex(@"[ \t\f\v]+")]
    private static partial Regex SpaceRuns();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessBlankLines();
}
