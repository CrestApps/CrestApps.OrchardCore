using System.Text;
using UglyToad.PdfPig;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Pdf;

public sealed class PdfDocumentTextExtractor : IDocumentTextExtractor
{
    public async Task<string> ExtractAsync(
        Stream stream,
        string fileName,
        string extension,
        string contentType
        )
    {
        if (stream is null || stream.Length == 0 || string.IsNullOrEmpty(extension) || string.IsNullOrEmpty(contentType))
        {
            return string.Empty;
        }

        if (!extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return await Task.Run(() =>
        {
            try
            {
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

                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        });
    }
}
