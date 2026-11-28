using System.Xml;
using CrestApps.OrchardCore.Tabular;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace CrestApps.OrchardCore.Surveys.Core.Imports;

/// <summary>
/// Reads the first sheet of an Excel workbook into named rows: the first row holds the column names. The sheet is read
/// a row at a time, every part is limited in size, columns stop at Excel's own limit and a data row keeps only the
/// columns the first row names, so a small crafted file can't make the reader use much memory.
/// </summary>
internal static class ExcelSheetReader
{
    /// <summary>
    /// The most columns a sheet has: Excel's own limit (column XFD).
    /// </summary>
    public const int MaxColumns = 16_384;

    /// <summary>
    /// The most characters a part of the workbook (a sheet, the shared strings) may hold once uncompressed.
    /// </summary>
    public const long MaxCharactersInPart = 20_000_000;

    /// <summary>
    /// The most cells read, empty cells before a value included.
    /// </summary>
    public const int MaxCells = 2_000_000;

    /// <summary>
    /// Reads the first sheet.
    /// </summary>
    /// <param name="stream">A seekable stream of the workbook.</param>
    /// <param name="maxRows">The most data rows read.</param>
    /// <param name="headers">The column names.</param>
    /// <param name="rows">The rows.</param>
    /// <param name="tooLarge">Set when the workbook is refused because it holds more than the reader reads.</param>
    /// <returns><see langword="true"/> when the file is a workbook that could be read.</returns>
    public static bool TryRead(Stream stream, int maxRows, out IReadOnlyList<string> headers, out IReadOnlyList<CsvRow> rows, out bool tooLarge)
    {
        headers = [];
        rows = [];
        tooLarge = false;

        SpreadsheetDocument document;

        try
        {
            document = SpreadsheetDocument.Open(stream, false, new OpenSettings { MaxCharactersInPart = MaxCharactersInPart });
        }
        catch (Exception exception) when (exception is OpenXmlPackageException or InvalidDataException or FileFormatException or XmlException)
        {
            return false;
        }

        using (document)
        {
            List<string[]> records;

            try
            {
                var workbook = document.WorkbookPart;
                var sheet = workbook?.Workbook?.Sheets?.Elements<Sheet>().FirstOrDefault();

                if (sheet?.Id?.Value is null || workbook.GetPartById(sheet.Id.Value) is not WorksheetPart worksheet)
                {
                    return true;
                }

                var sharedStrings = ReadSharedStrings(workbook.SharedStringTablePart);

                records = ReadRecords(worksheet, sharedStrings, maxRows, out tooLarge);
            }
            catch (Exception exception) when (exception is OpenXmlPackageException or InvalidDataException or FileFormatException or XmlException or InvalidOperationException)
            {
                // A part larger than MaxCharactersInPart, or XML that is not a sheet.
                return false;
            }

            if (tooLarge)
            {
                return false;
            }

            if (records.Count == 0)
            {
                return true;
            }

            headers = records[0].Select(header => header?.Trim() ?? string.Empty).ToArray();
            rows = CsvReader.ToRows(headers, records.Skip(1));
        }

        return true;
    }

    /// <summary>
    /// Gets the zero-based index of the column of a cell reference such as <c>B7</c>: <c>-1</c> when there is no
    /// reference, and <see cref="MaxColumns"/> or more when the column is past Excel's last column.
    /// </summary>
    /// <param name="reference">The cell reference.</param>
    /// <returns>The index.</returns>
    public static int ColumnIndex(string reference)
    {
        if (string.IsNullOrEmpty(reference))
        {
            return -1;
        }

        var index = 0;

        foreach (var character in reference)
        {
            if (!char.IsAsciiLetter(character))
            {
                break;
            }

            index = (index * 26) + (char.ToUpperInvariant(character) - 'A' + 1);

            // Past the last column: stop before the number can grow any further.
            if (index > MaxColumns)
            {
                return MaxColumns;
            }
        }

        return index == 0 ? -1 : index - 1;
    }

    private static string[] ReadSharedStrings(SharedStringTablePart part)
    {
        if (part is null)
        {
            return [];
        }

        var strings = new List<string>();

        using var reader = OpenXmlReader.Create(part);

        while (reader.Read())
        {
            if (reader.IsStartElement && reader.ElementType == typeof(SharedStringItem))
            {
                strings.Add(reader.LoadCurrentElement()?.InnerText ?? string.Empty);
            }
        }

        return [.. strings];
    }

    private static List<string[]> ReadRecords(WorksheetPart worksheet, string[] sharedStrings, int maxRows, out bool tooLarge)
    {
        tooLarge = false;

        var records = new List<string[]>();
        var cells = 0;

        // The first row names the columns: a data row keeps only those, so a far-away cell pads nothing.
        var width = MaxColumns;

        using var reader = OpenXmlReader.Create(worksheet);

        while (reader.Read())
        {
            if (!reader.IsStartElement || reader.ElementType != typeof(Row))
            {
                continue;
            }

            if (records.Count > maxRows)
            {
                break;
            }

            var row = (Row)reader.LoadCurrentElement();
            var values = new List<string>();

            foreach (var cell in row.Elements<Cell>())
            {
                var index = ColumnIndex(cell.CellReference?.Value);

                if (index < 0)
                {
                    index = values.Count;
                }

                if (index >= width)
                {
                    continue;
                }

                cells += Math.Max(index - values.Count, 0) + 1;

                if (cells > MaxCells)
                {
                    tooLarge = true;

                    return records;
                }

                while (values.Count < index)
                {
                    values.Add(string.Empty);
                }

                if (values.Count == index)
                {
                    values.Add(Value(cell, sharedStrings));
                }
                else
                {
                    // A cell that repeats an earlier column replaces it.
                    values[index] = Value(cell, sharedStrings);
                }
            }

            if (records.Count == 0)
            {
                width = Math.Max(values.Count, 1);
            }

            records.Add([.. values]);
        }

        return records;
    }

    private static string Value(Cell cell, string[] sharedStrings)
    {
        var text = cell.CellValue?.Text ?? cell.InnerText ?? string.Empty;

        if (cell.DataType?.Value == CellValues.SharedString && int.TryParse(text, out var index) && index >= 0 && index < sharedStrings.Length)
        {
            return sharedStrings[index];
        }

        if (cell.DataType?.Value == CellValues.InlineString)
        {
            return cell.InlineString?.InnerText ?? string.Empty;
        }

        return text;
    }
}
