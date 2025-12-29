using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Services;

/// <summary>
/// Service for extracting text content from uploaded documents.
/// </summary>
public interface IDocumentTextExtractor
{
    /// <summary>
    /// Extracts text content from a document stream.
    /// </summary>
    /// <param name="stream">The document stream.</param>
    /// <param name="fileName">The original file name with extension.</param>
    /// <param name="contentType">The content type of the file.</param>
    /// <returns>The extracted text content.</returns>
    Task<string> ExtractAsync(Stream stream, string fileName, string contentType);

    /// <summary>
    /// Checks if the file type is supported for text extraction.
    /// </summary>
    /// <param name="fileName">The file name with extension.</param>
    /// <param name="contentType">The content type.</param>
    /// <returns>True if supported, false otherwise.</returns>
    bool IsSupported(string fileName, string contentType);
}

/// <summary>
/// Default implementation of document text extraction.
/// Supports plain text files, PDF, and Office documents (Word, Excel, PowerPoint).
/// </summary>
public sealed class DefaultDocumentTextExtractor : IDocumentTextExtractor
{
    private static readonly HashSet<string> _textExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt",
        ".csv",
        ".md",
        ".json",
        ".xml",
        ".html",
        ".htm",
        ".log",
        ".yaml",
        ".yml",
    };

    private static readonly HashSet<string> _pdfExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf",
    };

    private static readonly HashSet<string> _wordExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".doc",
        ".docx",
    };

    private static readonly HashSet<string> _excelExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xls",
        ".xlsx",
    };

    private static readonly HashSet<string> _powerPointExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ppt",
        ".pptx",
    };

    private static readonly HashSet<string> _supportedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "text/plain",
        "text/csv",
        "text/markdown",
        "text/html",
        "text/xml",
        "application/json",
        "application/xml",
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "application/vnd.ms-powerpoint",
        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
    };

    public bool IsSupported(string fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName);

        return _textExtensions.Contains(extension) ||
               _pdfExtensions.Contains(extension) ||
               _wordExtensions.Contains(extension) ||
               _excelExtensions.Contains(extension) ||
               _powerPointExtensions.Contains(extension) ||
               _supportedContentTypes.Contains(contentType);
    }

    public async Task<string> ExtractAsync(Stream stream, string fileName, string contentType)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        // For text-based files, read directly
        if (IsTextFile(extension, contentType))
        {
            using var reader = new StreamReader(stream);
            return await reader.ReadToEndAsync();
        }

        // For PDF files
        if (_pdfExtensions.Contains(extension) || contentType == "application/pdf")
        {
            return await ExtractFromPdfAsync(stream);
        }

        // For Word documents
        if (_wordExtensions.Contains(extension) ||
            contentType == "application/msword" ||
            contentType == "application/vnd.openxmlformats-officedocument.wordprocessingml.document")
        {
            return await ExtractFromWordAsync(stream, extension);
        }

        // For Excel documents
        if (_excelExtensions.Contains(extension) ||
            contentType == "application/vnd.ms-excel" ||
            contentType == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
        {
            return await ExtractFromExcelAsync(stream, extension);
        }

        // For PowerPoint documents
        if (_powerPointExtensions.Contains(extension) ||
            contentType == "application/vnd.ms-powerpoint" ||
            contentType == "application/vnd.openxmlformats-officedocument.presentationml.presentation")
        {
            return await ExtractFromPowerPointAsync(stream, extension);
        }

        return string.Empty;
    }

    private static bool IsTextFile(string extension, string contentType)
    {
        return _textExtensions.Contains(extension) ||
               contentType?.StartsWith("text/", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static async Task<string> ExtractFromPdfAsync(Stream stream)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Copy stream to memory since PdfPig needs seekable stream
                using var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                using var document = PdfDocument.Open(memoryStream);
                var sb = new StringBuilder();

                foreach (Page page in document.GetPages())
                {
                    var text = page.Text;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sb.AppendLine(text);
                        sb.AppendLine();
                    }
                }

                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        });
    }

    private static async Task<string> ExtractFromWordAsync(Stream stream, string extension)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Only .docx format is supported by OpenXML
                if (!extension.Equals(".docx", StringComparison.OrdinalIgnoreCase))
                {
                    return "[Unsupported format: .doc files require legacy Office interop. Please convert to .docx]";
                }

                // Copy stream to memory since OpenXML needs seekable stream
                using var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                using var document = WordprocessingDocument.Open(memoryStream, false);
                var body = document.MainDocumentPart?.Document?.Body;

                if (body == null)
                {
                    return string.Empty;
                }

                var sb = new StringBuilder();
                foreach (var paragraph in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
                {
                    var text = paragraph.InnerText;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sb.AppendLine(text);
                    }
                }

                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        });
    }

    private static async Task<string> ExtractFromExcelAsync(Stream stream, string extension)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Only .xlsx format is supported by OpenXML
                if (!extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    return "[Unsupported format: .xls files require legacy Office interop. Please convert to .xlsx]";
                }

                // Copy stream to memory since OpenXML needs seekable stream
                using var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                using var document = SpreadsheetDocument.Open(memoryStream, false);
                var workbookPart = document.WorkbookPart;

                if (workbookPart == null)
                {
                    return string.Empty;
                }

                var sb = new StringBuilder();
                var sharedStringTable = workbookPart.SharedStringTablePart?.SharedStringTable;

                foreach (var worksheetPart in workbookPart.WorksheetParts)
                {
                    var sheetData = worksheetPart.Worksheet.GetFirstChild<SheetData>();
                    if (sheetData == null)
                    {
                        continue;
                    }

                    foreach (var row in sheetData.Elements<Row>())
                    {
                        var rowValues = new List<string>();

                        foreach (var cell in row.Elements<Cell>())
                        {
                            var value = GetCellValue(cell, sharedStringTable);
                            if (!string.IsNullOrEmpty(value))
                            {
                                rowValues.Add(value);
                            }
                        }

                        if (rowValues.Count > 0)
                        {
                            sb.AppendLine(string.Join("\t", rowValues));
                        }
                    }

                    sb.AppendLine();
                }

                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        });
    }

    private static string GetCellValue(Cell cell, SharedStringTable sharedStringTable)
    {
        if (cell.CellValue == null)
        {
            return string.Empty;
        }

        var value = cell.CellValue.InnerText;

        if (cell.DataType != null && cell.DataType.Value == CellValues.SharedString && sharedStringTable != null)
        {
            if (int.TryParse(value, out var index))
            {
                var item = sharedStringTable.ElementAtOrDefault(index);
                return item?.InnerText ?? value;
            }
        }

        return value;
    }

    private static async Task<string> ExtractFromPowerPointAsync(Stream stream, string extension)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Only .pptx format is supported by OpenXML
                if (!extension.Equals(".pptx", StringComparison.OrdinalIgnoreCase))
                {
                    return "[Unsupported format: .ppt files require legacy Office interop. Please convert to .pptx]";
                }

                // Copy stream to memory since OpenXML needs seekable stream
                using var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                memoryStream.Position = 0;

                using var document = PresentationDocument.Open(memoryStream, false);
                var presentationPart = document.PresentationPart;

                if (presentationPart == null)
                {
                    return string.Empty;
                }

                var sb = new StringBuilder();

                foreach (var slidePart in presentationPart.SlideParts)
                {
                    var slide = slidePart.Slide;
                    if (slide == null)
                    {
                        continue;
                    }

                    // Extract text from all text elements
                    foreach (var text in slide.Descendants<DocumentFormat.OpenXml.Drawing.Text>())
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
            catch
            {
                return string.Empty;
            }
        });
    }
}
