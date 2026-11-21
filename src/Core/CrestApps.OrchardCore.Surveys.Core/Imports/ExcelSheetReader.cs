using CrestApps.OrchardCore.Tabular;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace CrestApps.OrchardCore.Surveys.Core.Imports;

/// <summary>
/// Reads the first sheet of an Excel workbook into named rows: the first row holds the column names.
/// </summary>
internal static class ExcelSheetReader
{
    /// <summary>
    /// Reads the first sheet.
    /// </summary>
    /// <param name="stream">A seekable stream of the workbook.</param>
    /// <param name="maxRows">The most data rows read.</param>
    /// <param name="headers">The column names.</param>
    /// <param name="rows">The rows.</param>
    /// <returns><see langword="true"/> when the file is a workbook.</returns>
    public static bool TryRead(Stream stream, int maxRows, out IReadOnlyList<string> headers, out IReadOnlyList<CsvRow> rows)
    {
        headers = [];
        rows = [];

        SpreadsheetDocument document;

        try
        {
            document = SpreadsheetDocument.Open(stream, false);
        }
        catch (Exception exception) when (exception is OpenXmlPackageException or InvalidDataException or FileFormatException)
        {
            return false;
        }

        using (document)
        {
            var workbook = document.WorkbookPart;
            var sheet = workbook?.Workbook?.Sheets?.Elements<Sheet>().FirstOrDefault();

            if (sheet?.Id?.Value is null || workbook.GetPartById(sheet.Id.Value) is not WorksheetPart worksheet)
            {
                return true;
            }

            var sharedStrings = workbook.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>()
                .Select(item => item.InnerText)
                .ToArray() ?? [];

            var records = new List<string[]>();

            foreach (var row in worksheet.Worksheet.Descendants<Row>())
            {
                if (records.Count > maxRows)
                {
                    break;
                }

                var values = new List<string>();

                foreach (var cell in row.Elements<Cell>())
                {
                    var index = ColumnIndex(cell.CellReference?.Value);

                    while (values.Count < index)
                    {
                        values.Add(string.Empty);
                    }

                    values.Add(Value(cell, sharedStrings));
                }

                records.Add(values.ToArray());
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

    private static int ColumnIndex(string reference)
    {
        if (string.IsNullOrEmpty(reference))
        {
            return 0;
        }

        var index = 0;

        foreach (var character in reference)
        {
            if (!char.IsLetter(character))
            {
                break;
            }

            index = (index * 26) + (char.ToUpperInvariant(character) - 'A' + 1);
        }

        return Math.Max(0, index - 1);
    }
}
