using System.Net;
using System.Text;
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
    /// The most HTML converted, in characters. The text shown is cut to <see cref="EmailChannelConstants.MaxBodyLength"/>
    /// anyway, so the rest of a larger body would only cost time.
    /// </summary>
    internal const int MaxHtmlLength = 1_000_000;

    // The longest one pattern may take over the body. Markup built to make a pattern backtrack (thousands of unclosed
    // tags) could otherwise keep the receiver busy for hours.
    private const int MatchTimeoutMilliseconds = 1_000;

    // The elements that start a new line when the markup is only stripped.
    private static readonly HashSet<string> _blockElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "address", "article", "blockquote", "br", "div", "footer", "h1", "h2", "h3", "h4", "h5", "h6", "header", "hr",
        "li", "ol", "p", "pre", "section", "table", "tr", "ul",
    };

    // The elements whose content is never shown.
    private static readonly HashSet<string> _invisibleElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "head", "script", "style", "title",
    };

    /// <summary>
    /// Converts an HTML body to plain text.
    /// </summary>
    /// <param name="html">The HTML body. Only its first <see cref="MaxHtmlLength"/> characters are converted.</param>
    /// <returns>The plain text, or an empty string when <paramref name="html"/> is empty.</returns>
    public static string Convert(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        html = Shorten(html);

        try
        {
            return ConvertMarkup(html);
        }
        catch (RegexMatchTimeoutException)
        {
            // Markup that keeps a pattern busy this long is not a mail client's; dropping the tags takes one pass over
            // it, whatever it holds, and still leaves the words.
            return StripTags(html);
        }
    }

    /// <summary>
    /// Drops the markup without any pattern: the tags go, the content of the head, scripts and styles goes, block
    /// elements become line breaks, and entities are decoded. It takes one pass over the HTML whatever the markup.
    /// </summary>
    /// <param name="html">The HTML.</param>
    /// <returns>The plain text.</returns>
    internal static string StripTags(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(html.Length);
        var index = 0;

        while (index < html.Length)
        {
            var open = html.IndexOf('<', index);

            if (open < 0)
            {
                builder.Append(html, index, html.Length - index);

                break;
            }

            builder.Append(html, index, open - index);

            if (string.CompareOrdinal(html, open, "<!--", 0, 4) == 0)
            {
                var commentEnd = html.IndexOf("-->", open + 4, StringComparison.Ordinal);

                // A comment that is never closed hides the rest of the document, as it does in a browser.
                if (commentEnd < 0)
                {
                    break;
                }

                index = commentEnd + 3;

                continue;
            }

            var close = html.IndexOf('>', open + 1);

            // A tag that is never closed is not shown either.
            if (close < 0)
            {
                break;
            }

            var name = TagName(html, open + 1, close);

            if (_blockElements.Contains(name))
            {
                builder.Append('\n');
            }

            index = close + 1;

            // The content of a script or a style is skipped up to its end tag. One that is never closed hides the rest
            // of the document, so every search starts past the last one and the whole pass stays linear.
            if (html[open + 1] != '/' && _invisibleElements.Contains(name))
            {
                var end = html.IndexOf("</" + name, index, StringComparison.OrdinalIgnoreCase);

                if (end < 0)
                {
                    break;
                }

                var endClose = html.IndexOf('>', end);

                if (endClose < 0)
                {
                    break;
                }

                index = endClose + 1;
            }
        }

        var text = WebUtility.HtmlDecode(builder.ToString()).Replace('\u00A0', ' ');

        return Tidy(text);
    }

    // The original conversion, which keeps quotes, links and lists. Every pattern it runs has a time limit.
    private static string ConvertMarkup(string html)
    {
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

    // Keeps what fits under the limit, without leaving half a tag at the end to show as text.
    private static string Shorten(string html)
    {
        if (html.Length <= MaxHtmlLength)
        {
            return html;
        }

        var shortened = html.Substring(0, MaxHtmlLength);
        var lastOpen = shortened.LastIndexOf('<');

        return lastOpen > shortened.LastIndexOf('>')
            ? shortened.Substring(0, lastOpen)
            : shortened;
    }

    // The element name of the tag between the brackets: "p" for "<p class=x>" and for "</p>".
    private static string TagName(string html, int start, int end)
    {
        if (start < end && html[start] == '/')
        {
            start++;
        }

        var length = 0;

        while (start + length < end && char.IsAsciiLetterOrDigit(html[start + length]))
        {
            length++;
        }

        return html.Substring(start, length);
    }

    // Collapses the white space the markup left behind and keeps at most one blank line between paragraphs.
    private static string Tidy(string text)
    {
        var output = new StringBuilder(text.Length);
        var blankLines = 0;

        foreach (var rawLine in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = CollapseSpaces(rawLine);

            if (line.Length == 0)
            {
                blankLines++;

                continue;
            }

            if (output.Length > 0)
            {
                output.Append(blankLines > 0 ? "\n\n" : "\n");
            }

            output.Append(line);
            blankLines = 0;
        }

        return output.ToString();
    }

    private static string CollapseSpaces(string line)
    {
        var builder = new StringBuilder(line.Length);
        var pendingSpace = false;

        foreach (var character in line)
        {
            if (character is ' ' or '\t' or '\f' or '\v')
            {
                pendingSpace = builder.Length > 0;

                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        return builder.ToString();
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

    [GeneratedRegex(@"<(head|script|style|title)\b[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline, MatchTimeoutMilliseconds)]
    private static partial Regex Invisible();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline, MatchTimeoutMilliseconds)]
    private static partial Regex Comments();

    [GeneratedRegex(@"<(blockquote\b[^>]*|div\b[^>]*class\s*=\s*[""'][^""']*(gmail_quote|yahoo_quoted|moz-cite-prefix)[^""']*[""'][^>]*)>", RegexOptions.IgnoreCase, MatchTimeoutMilliseconds)]
    private static partial Regex QuoteStart();

    [GeneratedRegex(@"</blockquote\s*>", RegexOptions.IgnoreCase, MatchTimeoutMilliseconds)]
    private static partial Regex QuoteEnd();

    [GeneratedRegex(@"<a\b[^>]*\bhref\s*=\s*[""'](?<href>[^""']*)[""'][^>]*>(?<label>.*?)</a\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline, MatchTimeoutMilliseconds)]
    private static partial Regex Links();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase, MatchTimeoutMilliseconds)]
    private static partial Regex LineBreaks();

    [GeneratedRegex(@"</(p|div|tr|table|h[1-6]|ul|ol|li|section|article|header|footer)\s*>|<(p|div|tr|table|h[1-6]|hr)\b[^>]*>", RegexOptions.IgnoreCase, MatchTimeoutMilliseconds)]
    private static partial Regex BlockEnds();

    [GeneratedRegex(@"<li\b[^>]*>", RegexOptions.IgnoreCase, MatchTimeoutMilliseconds)]
    private static partial Regex ListItems();

    [GeneratedRegex(@"<[^>]+>", RegexOptions.Singleline, MatchTimeoutMilliseconds)]
    private static partial Regex Tags();

    [GeneratedRegex(@"[ \t\f\v]+", RegexOptions.None, MatchTimeoutMilliseconds)]
    private static partial Regex SpaceRuns();

    [GeneratedRegex(@"\n{3,}", RegexOptions.None, MatchTimeoutMilliseconds)]
    private static partial Regex ExcessBlankLines();
}
