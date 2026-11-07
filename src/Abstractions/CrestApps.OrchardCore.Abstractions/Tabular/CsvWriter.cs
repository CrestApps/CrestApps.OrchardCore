using System.Globalization;
using System.Text;

namespace CrestApps.OrchardCore.Tabular;

/// <summary>
/// Writes CSV the way RFC 4180 describes it: cells separated by commas, records ended by CRLF, and a cell quoted when it
/// holds a comma, a double quote or a line break, with its quotes doubled. A cell that a spreadsheet would run as a
/// formula (it starts with <c>=</c>, <c>+</c>, <c>-</c>, <c>@</c>, a tab or a carriage return) gets a leading
/// apostrophe, unless it is a number such as <c>-5.00</c> or <c>+15551234567</c>.
/// </summary>
/// <remarks>
/// Use it for every CSV a module writes, so no export forgets the escaping or the formula guard. Read CSV with
/// <see cref="CsvReader"/>.
/// </remarks>
public static class CsvWriter
{
    /// <summary>
    /// The characters that end a record.
    /// </summary>
    public const string RecordSeparator = "\r\n";

    private static readonly char[] _charactersThatNeedQuotes = [',', '"', '\r', '\n'];

    /// <summary>
    /// Escapes a cell, with the formula guard.
    /// </summary>
    /// <param name="value">The cell value; <see langword="null"/> writes an empty cell.</param>
    /// <returns>The cell as it goes in the file.</returns>
    public static string Escape(string value)
        => Escape(value, guardFormula: true);

    /// <summary>
    /// Escapes a cell.
    /// </summary>
    /// <param name="value">The cell value; <see langword="null"/> writes an empty cell.</param>
    /// <param name="guardFormula">Whether a value that a spreadsheet would run as a formula gets a leading apostrophe.
    /// Pass <see langword="false"/> only for a file that a machine, not a spreadsheet, reads, such as a product feed.</param>
    /// <returns>The cell as it goes in the file.</returns>
    public static string Escape(string value, bool guardFormula)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (guardFormula && StartsLikeAFormula(value))
        {
            value = "'" + value;
        }

        return value.IndexOfAny(_charactersThatNeedQuotes) >= 0
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;
    }

    /// <summary>
    /// Appends one record, with the formula guard, ended by <see cref="RecordSeparator"/>.
    /// </summary>
    /// <param name="builder">The CSV being built.</param>
    /// <param name="values">The cells of the record.</param>
    public static void AppendRow(StringBuilder builder, params IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AppendJoin(',', (values ?? []).Select(Escape)).Append(RecordSeparator);
    }

    /// <summary>
    /// Writes one record, with the formula guard, ended by <see cref="RecordSeparator"/>.
    /// </summary>
    /// <param name="writer">The writer of the file.</param>
    /// <param name="values">The cells of the record.</param>
    public static void WriteRow(TextWriter writer, params IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(writer);

        var first = true;

        foreach (var value in values ?? [])
        {
            if (!first)
            {
                writer.Write(',');
            }

            first = false;
            writer.Write(Escape(value));
        }

        writer.Write(RecordSeparator);
    }

    /// <summary>
    /// Returns whether a value starts like a spreadsheet formula and is not a number.
    /// </summary>
    /// <param name="value">The cell value.</param>
    public static bool StartsLikeAFormula(string value)
    {
        if (string.IsNullOrEmpty(value) || value[0] is not ('=' or '+' or '-' or '@' or '\t' or '\r'))
        {
            return false;
        }

        return !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _);
    }
}
