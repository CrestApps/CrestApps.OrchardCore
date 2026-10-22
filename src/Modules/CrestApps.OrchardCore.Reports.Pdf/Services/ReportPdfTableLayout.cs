using CrestApps.OrchardCore.Reports.Models;
using PdfSharp.Drawing;

namespace CrestApps.OrchardCore.Reports.Pdf.Services;

/// <summary>
/// Measures the columns of a report table with the font it is written in, and shares the width of the page between
/// them. Each column needs at least the width of its longest word, since a word does not wrap, and would like the
/// width of its longest value; a column of long names gets more room than a column of counts.
/// </summary>
internal sealed class ReportPdfTableLayout
{
    /// <summary>
    /// The left and right padding of a cell, together, in points.
    /// </summary>
    public const double CellPadding = 8;

    // The widest value a column asks room for, in characters: a longer value wraps rather than squeezing the others.
    private const int MaxPreferredCharacters = 40;

    // The longest word a column asks room for, in characters: a longer one, such as a URL, is broken to fit.
    private const int MaxWordCharacters = 24;

    // The rows read to measure a table: enough to be representative without measuring every row of a large export.
    private const int SampledRows = 200;

    private readonly double[] _minimum;
    private readonly double[] _preferred;
    private readonly double[] _characterWidth;

    private ReportPdfTableLayout(double fontSize, double[] minimum, double[] preferred, double[] characterWidth)
    {
        FontSize = fontSize;
        _minimum = minimum;
        _preferred = preferred;
        _characterWidth = characterWidth;
    }

    /// <summary>
    /// Gets the number of columns.
    /// </summary>
    public int ColumnCount => _minimum.Length;

    /// <summary>
    /// Gets the font size of the table, smaller for tables with many columns.
    /// </summary>
    public double FontSize { get; }

    /// <summary>
    /// Gets the width under which a word of the table no longer fits its column, in points.
    /// </summary>
    public double MinimumWidth => _minimum.Sum();

    /// <summary>
    /// Gets the width at which every value of the table fits on one line, in points.
    /// </summary>
    public double PreferredWidth => _preferred.Sum();

    /// <summary>
    /// Measures a table section.
    /// </summary>
    /// <param name="section">The table section.</param>
    /// <param name="graphics">A context to measure text with.</param>
    /// <param name="fontFamily">The font the table is written in.</param>
    /// <param name="baseFontSize">The font size of the text of the report.</param>
    /// <returns>The layout, or <see langword="null"/> when the table has no column.</returns>
    public static ReportPdfTableLayout Measure(ReportSection section, XGraphics graphics, string fontFamily, double baseFontSize)
    {
        var columns = section.Columns ?? [];
        var rows = (section.Rows ?? []).Where(row => row is not null).ToList();
        var columnCount = GetColumnCount(columns, rows);

        if (columnCount == 0)
        {
            return null;
        }

        var fontSize = columnCount switch
        {
            > 14 => baseFontSize - 3,
            > 10 => baseFontSize - 2,
            _ => baseFontSize - 1,
        };

        // Bold is measured throughout: headers and totals are bold, and a little room to spare costs nothing.
        var font = new XFont(fontFamily, fontSize, XFontStyleEx.Bold);
        var minimum = new double[columnCount];
        var preferred = new double[columnCount];
        var characterWidth = new double[columnCount];

        for (var index = 0; index < columnCount; index++)
        {
            var header = index < columns.Count ? columns[index]?.Label : null;
            var longestWord = LongestWord(header);
            var longestHeaderWord = longestWord;
            var longestValue = string.Empty;

            foreach (var row in rows.Take(SampledRows))
            {
                if (row.Cells is not { } cells || index >= cells.Count || string.IsNullOrEmpty(cells[index]))
                {
                    continue;
                }

                foreach (var line in cells[index].Split('\n'))
                {
                    if (line.Length > longestValue.Length)
                    {
                        longestValue = line;
                    }
                }

                var word = LongestWord(cells[index]);

                if (word.Length > longestWord.Length)
                {
                    longestWord = word;
                }
            }

            var measuredWord = longestWord.Length > MaxWordCharacters ? longestWord[..MaxWordCharacters] : longestWord;
            var measuredValue = longestValue.Length > MaxPreferredCharacters ? longestValue[..MaxPreferredCharacters] : longestValue;
            var wordWidth = Width(graphics, measuredWord, font);

            // A header wraps between its words, but asks for its whole width when its column is one of short values.
            minimum[index] = Math.Max(wordWidth, Width(graphics, longestHeaderWord, font)) + CellPadding;
            preferred[index] = Math.Max(minimum[index], Math.Max(Width(graphics, measuredValue, font), Width(graphics, header ?? string.Empty, font)) + CellPadding);
            characterWidth[index] = measuredWord.Length > 0 ? wordWidth / measuredWord.Length : fontSize * 0.6;
        }

        return new ReportPdfTableLayout(fontSize, minimum, preferred, characterWidth);
    }

    /// <summary>
    /// Counts the columns of a table: its headers, or its widest row when it has more cells than headers.
    /// </summary>
    /// <param name="columns">The columns.</param>
    /// <param name="rows">The rows.</param>
    /// <returns>The number of columns.</returns>
    public static int GetColumnCount(IList<ReportColumn> columns, IList<ReportRow> rows)
    {
        return Math.Max(columns.Count, rows.Count == 0 ? 0 : rows.Max(row => row.Cells?.Count ?? 0));
    }

    /// <summary>
    /// Shares a width between the columns. When every value fits, the columns grow in proportion to what they would
    /// like; otherwise each gets its minimum and the rest goes to the columns whose values wrap.
    /// </summary>
    /// <param name="width">The width of the table, in points.</param>
    /// <returns>The width of each column, in points.</returns>
    public double[] GetColumnWidths(double width)
    {
        var preferred = PreferredWidth;

        if (preferred <= width)
        {
            return [.. _preferred.Select(column => column * width / preferred)];
        }

        var minimum = MinimumWidth;

        if (minimum >= width)
        {
            return [.. _minimum.Select(column => column * width / minimum)];
        }

        var flexible = preferred - minimum;

        return [.. _minimum.Select((column, index) => column + ((width - minimum) * (_preferred[index] - column) / flexible))];
    }

    /// <summary>
    /// Gets how many characters of a word fit on a line of each column; a longer word is broken.
    /// </summary>
    /// <param name="widths">The widths of the columns, in points.</param>
    /// <returns>The characters per line of each column.</returns>
    public int[] GetCharactersPerLine(double[] widths)
    {
        return [.. widths.Select((width, index) => Math.Max(1, (int)Math.Floor((width - CellPadding) / _characterWidth[index])))];
    }

    private static double Width(XGraphics graphics, string text, XFont font)
    {
        return string.IsNullOrEmpty(text) ? 0 : graphics.MeasureString(text, font).Width;
    }

    private static string LongestWord(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries)
            .MaxBy(word => word.Length) ?? string.Empty;
    }
}
