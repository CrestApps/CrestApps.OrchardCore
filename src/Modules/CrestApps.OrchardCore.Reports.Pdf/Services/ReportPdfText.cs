using System.Text;

namespace CrestApps.OrchardCore.Reports.Pdf.Services;

/// <summary>
/// Cleans and wraps the text of a report PDF.
/// </summary>
internal static class ReportPdfText
{
    /// <summary>
    /// Keeps the line breaks of a value and turns the other control characters, which a font has no glyph for, into
    /// spaces.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The value with <c>\n</c> line breaks and no other control character.</returns>
    public static string Clean(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];

            if (character == '\r')
            {
                if (index + 1 < value.Length && value[index + 1] == '\n')
                {
                    continue;
                }

                builder.Append('\n');
            }
            else if (character == '\n')
            {
                builder.Append('\n');
            }
            else
            {
                builder.Append(char.IsControl(character) ? ' ' : character);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Breaks prepared text into the lines that fit a width: at its line breaks, between its words, and inside a word
    /// that is wider than the line, such as a URL.
    /// </summary>
    /// <param name="text">The text, from <see cref="ReportPdfFont.Prepare"/>.</param>
    /// <param name="font">The font of the text.</param>
    /// <param name="size">The font size, in points.</param>
    /// <param name="width">The width of a line, in points.</param>
    /// <returns>The lines; none when there is no text.</returns>
    public static List<string> Wrap(string text, ReportPdfFont font, double size, double width)
    {
        var lines = new List<string>();

        if (string.IsNullOrEmpty(text))
        {
            return lines;
        }

        foreach (var paragraph in text.Split('\n'))
        {
            WrapParagraph(paragraph, font, size, width, lines);
        }

        return lines;
    }

    /// <summary>
    /// Shortens prepared text, without line breaks, to a width, ending it with an ellipsis when it is cut.
    /// </summary>
    /// <param name="text">The text, from <see cref="ReportPdfFont.Prepare"/>.</param>
    /// <param name="font">The font of the text.</param>
    /// <param name="size">The font size, in points.</param>
    /// <param name="width">The width, in points.</param>
    /// <returns>The text, whole when it fits.</returns>
    public static string Fit(string text, ReportPdfFont font, double size, double width)
    {
        if (string.IsNullOrEmpty(text) || font.Measure(text, size) <= width)
        {
            return text;
        }

        var ellipsis = font.Supports('…') ? "…" : "...";
        var available = width - font.Measure(ellipsis, size);
        var length = 0;
        var used = 0d;

        while (length < text.Length)
        {
            var next = font.Measure(text[length], size);

            if (used + next > available)
            {
                break;
            }

            used += next;
            length++;
        }

        return text[..length].TrimEnd() + ellipsis;
    }

    private static void WrapParagraph(string paragraph, ReportPdfFont font, double size, double width, List<string> lines)
    {
        if (font.Measure(paragraph, size) <= width)
        {
            lines.Add(paragraph);

            return;
        }

        var spaceWidth = font.Measure(" ", size);
        var line = new StringBuilder();
        var lineWidth = 0d;

        foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var wordWidth = font.Measure(word, size);

            if (line.Length > 0 && lineWidth + spaceWidth + wordWidth <= width)
            {
                line.Append(' ').Append(word);
                lineWidth += spaceWidth + wordWidth;

                continue;
            }

            if (line.Length > 0)
            {
                lines.Add(line.ToString());
                line.Clear();
                lineWidth = 0;
            }

            if (wordWidth <= width)
            {
                line.Append(word);
                lineWidth = wordWidth;

                continue;
            }

            // A word wider than the line is broken where the line is full; its last part starts the next line.
            foreach (var character in word)
            {
                var characterWidth = font.Measure(character, size);

                if (line.Length > 0 && lineWidth + characterWidth > width)
                {
                    lines.Add(line.ToString());
                    line.Clear();
                    lineWidth = 0;
                }

                line.Append(character);
                lineWidth += characterWidth;
            }
        }

        if (line.Length > 0 || lines.Count == 0)
        {
            lines.Add(line.ToString());
        }
    }
}
