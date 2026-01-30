using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.OpenXml;

public sealed class OpenXmlDocumentTextExtractor : IDocumentTextExtractor
{
    public Task<string> ExtractAsync(
        Stream stream,
        string fileName,
        string extension,
        string contentType)
    {
        if (stream is null || stream.Length == 0 || string.IsNullOrEmpty(extension))
        {
            return Task.FromResult(string.Empty);
        }

        // OpenXml operations are synchronous and typically fast.
        // Since we're already in an async context, we extract synchronously.
        var result = extension.ToLowerInvariant() switch
        {
            ".docx" => ExtractWord(stream),
            ".xlsx" => ExtractExcel(stream),
            ".pptx" => ExtractPowerPoint(stream),
            _ => string.Empty
        };

        return Task.FromResult(result);
    }

    private static string ExtractWord(Stream stream)
    {
        using var memory = CopyToMemory(stream);
        using var document = WordprocessingDocument.Open(memory, false);

        var body = document.MainDocumentPart?.Document?.Body;

        if (body == null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        foreach (var paragraph in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
        {
            if (!string.IsNullOrWhiteSpace(paragraph.InnerText))
            {
                sb.AppendLine(paragraph.InnerText);
            }
        }

        return sb.ToString();
    }

    private static string ExtractExcel(Stream stream)
    {
        using var memory = CopyToMemory(stream);
        using var document = SpreadsheetDocument.Open(memory, false);

        var workbook = document.WorkbookPart;
        if (workbook == null)
        {
            return string.Empty;
        }

        var sharedStrings = workbook.SharedStringTablePart?.SharedStringTable;
        var sb = new StringBuilder();

        foreach (var sheet in workbook.WorksheetParts)
        {
            var data = sheet.Worksheet.GetFirstChild<SheetData>();
            if (data == null)
            {
                continue;
            }

            foreach (var row in data.Elements<Row>())
            {
                var values = row.Elements<Cell>()
                    .Select(c => GetCellValue(c, sharedStrings))
                    .Where(v => !string.IsNullOrEmpty(v));

                if (values.Any())
                {
                    sb.AppendLine(string.Join("\t", values));
                }
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string ExtractPowerPoint(Stream stream)
    {
        using var memory = CopyToMemory(stream);
        using var document = PresentationDocument.Open(memory, false);

        var presentation = document.PresentationPart;
        if (presentation == null)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();

        foreach (var slide in presentation.SlideParts)
        {
            foreach (var text in slide.Slide.Descendants<DocumentFormat.OpenXml.Drawing.Text>())
            {
                if (!string.IsNullOrWhiteSpace(text.Text))
                {
                    sb.AppendLine(text.Text);
                }
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static MemoryStream CopyToMemory(Stream stream)
    {
        var memory = new MemoryStream();
        stream.CopyTo(memory);
        memory.Position = 0;
        return memory;
    }

    private static string GetCellValue(Cell cell, SharedStringTable table)
    {
        if (cell.CellValue == null)
        {
            return string.Empty;
        }

        var value = cell.CellValue.InnerText;

        if (cell.DataType?.Value == CellValues.SharedString &&
            int.TryParse(value, out var index))
        {
            return table?.ElementAtOrDefault(index)?.InnerText ?? value;
        }

        return value;
    }
}
