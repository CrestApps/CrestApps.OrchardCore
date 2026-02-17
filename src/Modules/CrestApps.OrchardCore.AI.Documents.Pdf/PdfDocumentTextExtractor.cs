using System.Text;
using UglyToad.PdfPig;

namespace CrestApps.OrchardCore.AI.Documents.Pdf;

public sealed class PdfDocumentTextExtractor : IDocumentTextExtractor
{
    public Task<string> ExtractAsync(
        Stream stream,
        string fileName,
        string extension,
        string contentType)
    {
        if (stream is null || stream.Length == 0 || string.IsNullOrEmpty(extension) || string.IsNullOrEmpty(contentType))
        {
            return Task.FromResult(string.Empty);
        }

        if (!extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(string.Empty);
        }

        try
        {
            // PdfPig is CPU-bound synchronous code. Since extraction is typically fast
            // and we're already in an async context, we extract synchronously.
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            memory.Position = 0;

            using var document = PdfDocument.Open(memory);
            var sb = new StringBuilder();

            foreach (var page in document.GetPages())
            {
                if (!string.IsNullOrWhiteSpace(page.Text))
                {
                    sb.AppendLine(page.Text);
                    sb.AppendLine();
                }
            }

            return Task.FromResult(sb.ToString());
        }
        catch
        {
            return Task.FromResult(string.Empty);
        }
    }
}
