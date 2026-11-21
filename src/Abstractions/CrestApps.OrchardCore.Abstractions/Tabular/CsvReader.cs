using System.Text;

namespace CrestApps.OrchardCore.Tabular;

/// <summary>
/// Reads the CSV that RFC 4180 describes, and the files spreadsheets save: cells separated by commas, records ended by
/// CRLF or LF, and quoted cells that may hold commas, doubled quotes and line breaks. A byte order mark at the start is
/// skipped. Write CSV with <see cref="CsvWriter"/>.
/// </summary>
public static class CsvReader
{
    private const char ByteOrderMark = '﻿';

    /// <summary>
    /// Takes off the apostrophe <see cref="CsvWriter"/> puts in front of a cell a spreadsheet would run as a formula, so
    /// a file that was exported and imported again keeps its values. The writer also guards a value that itself starts
    /// with an apostrophe and a formula character, so exactly one apostrophe comes off. An apostrophe in front of
    /// anything else is part of the value and stays.
    /// </summary>
    /// <param name="value">The cell as it was read.</param>
    /// <returns>The value.</returns>
    /// <remarks>
    /// A file written elsewhere whose cell starts with an apostrophe and a formula character, such as <c>'=x</c>, loses
    /// that apostrophe, as it would were it this writer's guard.
    /// </remarks>
    public static string RemoveFormulaGuard(string value)
        => value is { Length: > 1 } && value[0] == '\'' && CsvWriter.NeedsFormulaGuard(value[1..]) ? value[1..] : value;

    /// <summary>
    /// Reads the records of a file one at a time, so a large file is never held in memory. An empty line reads as one
    /// empty cell.
    /// </summary>
    /// <param name="reader">The reader of the file.</param>
    /// <returns>The records, each as its cells.</returns>
    public static IEnumerable<string[]> ReadRecords(TextReader reader)
        => ReadRecords(reader, int.MaxValue);

    /// <summary>
    /// Reads the records of a file one at a time, refusing a cell longer than a limit. A quote that is never closed
    /// would otherwise read the rest of the file into one cell.
    /// </summary>
    /// <param name="reader">The reader of the file.</param>
    /// <param name="maxCellLength">The most characters a cell may hold.</param>
    /// <returns>The records, each as its cells.</returns>
    /// <exception cref="InvalidOperationException">A cell holds more than <paramref name="maxCellLength"/> characters.</exception>
    public static IEnumerable<string[]> ReadRecords(TextReader reader, int maxCellLength)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCellLength);

        return ReadRecordsIterator(reader, maxCellLength);
    }

    /// <summary>
    /// Reads every record of a CSV text. An empty line reads as one empty cell.
    /// </summary>
    /// <param name="text">The CSV text.</param>
    /// <returns>The records, each as its cells.</returns>
    public static IReadOnlyList<string[]> ParseRecords(string text)
    {
        using var reader = new StringReader(text ?? string.Empty);

        return [.. ReadRecordsIterator(reader, int.MaxValue)];
    }

    /// <summary>
    /// Reads the cells of one line, for files read line by line. A quoted cell may hold commas and doubled quotes, but
    /// not a line break.
    /// </summary>
    /// <param name="line">The line, without its line break.</param>
    /// <returns>The cells of the line.</returns>
    public static string[] ParseLine(string line)
    {
        using var reader = new StringReader(line ?? string.Empty);

        return ReadRecord(reader, skipByteOrderMark: true, int.MaxValue) ?? [string.Empty];
    }

    /// <summary>
    /// Reads a CSV text whose first record names the columns.
    /// </summary>
    /// <param name="text">The CSV text.</param>
    /// <returns>The trimmed column names, and the rows that hold a value, read by column name.</returns>
    public static (IReadOnlyList<string> Headers, IReadOnlyList<CsvRow> Rows) Parse(string text)
    {
        var records = ParseRecords(text);

        if (records.Count == 0)
        {
            return ([], []);
        }

        var headers = records[0].Select(header => header.Trim()).ToArray();

        return (headers, ToRows(headers, records.Skip(1)));
    }

    /// <summary>
    /// Turns the records under a header into rows read by column name, leaving out the rows that hold no value. Row
    /// numbers count the header as 1.
    /// </summary>
    /// <param name="headers">The column names.</param>
    /// <param name="records">The records under the header.</param>
    /// <returns>The rows.</returns>
    public static IReadOnlyList<CsvRow> ToRows(IReadOnlyList<string> headers, IEnumerable<string[]> records)
    {
        var names = (headers ?? []).Select(header => header?.Trim()).ToArray();
        var rows = new List<CsvRow>();
        var number = 1;

        foreach (var record in records ?? [])
        {
            number++;

            if (record is null || record.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (var column = 0; column < names.Length && column < record.Length; column++)
            {
                if (!string.IsNullOrEmpty(names[column]))
                {
                    values.TryAdd(names[column], record[column]);
                }
            }

            rows.Add(new CsvRow(number, values));
        }

        return rows;
    }

    private static IEnumerable<string[]> ReadRecordsIterator(TextReader reader, int maxCellLength)
    {
        var first = true;

        while (ReadRecord(reader, skipByteOrderMark: first, maxCellLength) is { } record)
        {
            first = false;

            yield return record;
        }
    }

    // One record, which a quoted cell may spread over several lines; null at the end of the text.
    private static string[] ReadRecord(TextReader reader, bool skipByteOrderMark, int maxCellLength)
    {
        if (skipByteOrderMark && reader.Peek() == ByteOrderMark)
        {
            reader.Read();
        }

        if (reader.Peek() < 0)
        {
            return null;
        }

        var cells = new List<string>();
        var cell = new StringBuilder();
        var quoted = false;

        while (true)
        {
            var next = reader.Read();

            if (next < 0)
            {
                cells.Add(cell.ToString());

                return [.. cells];
            }

            var character = (char)next;

            // Checked as the cell grows, so a quote that is never closed stops at the limit instead of reading the file.
            if (cell.Length > maxCellLength)
            {
                throw new InvalidOperationException($"A cell of the file holds more than {maxCellLength} characters, the most an import may have.");
            }

            if (quoted)
            {
                if (character != '"')
                {
                    cell.Append(character);
                }
                else if (reader.Peek() == '"')
                {
                    reader.Read();
                    cell.Append('"');
                }
                else
                {
                    quoted = false;
                }

                continue;
            }

            switch (character)
            {
                case '"':
                    quoted = true;
                    break;
                case ',':
                    cells.Add(cell.ToString());
                    cell.Clear();
                    break;
                case '\r':
                    if (reader.Peek() == '\n')
                    {
                        reader.Read();
                    }

                    cells.Add(cell.ToString());

                    return [.. cells];
                case '\n':
                    cells.Add(cell.ToString());

                    return [.. cells];
                default:
                    cell.Append(character);
                    break;
            }
        }
    }
}
