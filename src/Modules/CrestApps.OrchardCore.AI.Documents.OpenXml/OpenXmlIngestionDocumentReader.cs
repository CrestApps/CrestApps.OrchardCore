using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.Extensions.DataIngestion;

namespace CrestApps.OrchardCore.AI.Documents.OpenXml;

/// <summary>
/// Reads Word, Excel, and PowerPoint files into an <see cref="IngestionDocument"/>
/// using the Open XML SDK. Only supports the Office Open XML formats (.docx, .xlsx, .pptx).
/// </summary>
internal sealed class OpenXmlIngestionDocumentReader : IngestionDocumentReader
{
    private static readonly HashSet<string> _supportedMediaTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
    };

    public override async Task<IngestionDocument> ReadAsync(
        Stream source,
        string identifier,
        string mediaType,
        CancellationToken cancellationToken = default)
    {
        if (!_supportedMediaTypes.Contains(mediaType))
        {
            throw new NotSupportedException($"Media type '{mediaType}' is not supported by the OpenXml reader.");
        }

        var document = new IngestionDocument(identifier);

        // OpenXml requires a seekable stream. Avoid copying when already seekable.
        Stream workingStream;
        MemoryStream buffer = null;

        if (source.CanSeek)
        {
            source.Position = 0;
            workingStream = source;
        }
        else
        {
            buffer = new MemoryStream();
            await source.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            workingStream = buffer;
        }

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var section = mediaType switch
            {
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ExtractWord(workingStream, cancellationToken),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ExtractExcel(workingStream, cancellationToken),
                "application/vnd.openxmlformats-officedocument.presentationml.presentation" => ExtractPowerPoint(workingStream, cancellationToken),
                _ => null,
            };

            if (section != null)
            {
                document.Sections.Add(section);
            }
        }
        finally
        {
            if (buffer != null)
            {
                await buffer.DisposeAsync();
            }
        }

        return document;
    }

    private static IngestionDocumentSection ExtractWord(Stream stream, CancellationToken cancellationToken)
    {
        using var doc = WordprocessingDocument.Open(stream, false);

        var body = doc.MainDocumentPart?.Document?.Body;
        if (body == null)
        {
            return null;
        }

        var section = new IngestionDocumentSection();

        foreach (var paragraph in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!string.IsNullOrWhiteSpace(paragraph.InnerText))
            {
                section.Elements.Add(new IngestionDocumentParagraph(paragraph.InnerText)
                {
                    Text = paragraph.InnerText,
                });
            }
        }

        return section.Elements.Count > 0 ? section : null;
    }

    private static IngestionDocumentSection ExtractExcel(Stream stream, CancellationToken cancellationToken)
    {
        using var doc = SpreadsheetDocument.Open(stream, false);

        var workbook = doc.WorkbookPart;
        if (workbook == null)
        {
            return null;
        }

        var sharedStrings = workbook.SharedStringTablePart?.SharedStringTable;
        var section = new IngestionDocumentSection();

        foreach (var sheet in workbook.WorksheetParts)
        {
            cancellationToken.ThrowIfCancellationRequested();

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
                    var rowText = string.Join("\t", values);
                    section.Elements.Add(new IngestionDocumentParagraph(rowText)
                    {
                        Text = rowText,
                    });
                }
            }
        }

        return section.Elements.Count > 0 ? section : null;
    }

    private static IngestionDocumentSection ExtractPowerPoint(Stream stream, CancellationToken cancellationToken)
    {
        using var doc = PresentationDocument.Open(stream, false);

        var presentation = doc.PresentationPart;
        if (presentation == null)
        {
            return null;
        }

        var section = new IngestionDocumentSection();
        var sb = new StringBuilder();

        foreach (var slide in presentation.SlideParts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            sb.Clear();

            foreach (var text in slide.Slide.Descendants<DocumentFormat.OpenXml.Drawing.Text>())
            {
                if (!string.IsNullOrWhiteSpace(text.Text))
                {
                    sb.AppendLine(text.Text);
                }
            }

            if (sb.Length > 0)
            {
                var slideText = sb.ToString().TrimEnd();
                section.Elements.Add(new IngestionDocumentParagraph(slideText)
                {
                    Text = slideText,
                });
            }
        }

        return section.Elements.Count > 0 ? section : null;
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
