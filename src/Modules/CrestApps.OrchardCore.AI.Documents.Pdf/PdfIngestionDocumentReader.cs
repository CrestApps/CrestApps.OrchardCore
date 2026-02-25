using Microsoft.Extensions.DataIngestion;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace CrestApps.OrchardCore.AI.Documents.Pdf;

/// <summary>
/// Reads PDF files into an <see cref="IngestionDocument"/> using PdfPig,
/// producing one section per page with paragraph-level text blocks.
/// Only supports text-based PDFs; scanned/image-only PDFs will produce empty results.
/// </summary>
internal sealed class PdfIngestionDocumentReader : IngestionDocumentReader
{
    private const string PdfMediaType = "application/pdf";

    public override async Task<IngestionDocument> ReadAsync(
        Stream source,
        string identifier,
        string mediaType,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(mediaType, PdfMediaType, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"Media type '{mediaType}' is not supported. Only '{PdfMediaType}' is accepted.");
        }

        // PdfPig requires a seekable stream. Avoid copying when already seekable.
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
            using var pdf = PdfDocument.Open(workingStream);
            var document = new IngestionDocument(identifier);

            foreach (var page in pdf.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();

                var section = GetPageSection(page);
                if (section.Elements.Count > 0)
                {
                    document.Sections.Add(section);
                }
            }

            return document;
        }
        finally
        {
            if (buffer != null)
            {
                await buffer.DisposeAsync();
            }
        }
    }

    private static IngestionDocumentSection GetPageSection(Page pdfPage)
    {
        var section = new IngestionDocumentSection
        {
            PageNumber = pdfPage.Number,
        };

        var letters = pdfPage.Letters;
        var words = NearestNeighbourWordExtractor.Instance.GetWords(letters);

        foreach (var textBlock in DocstrumBoundingBoxes.Instance.GetBlocks(words))
        {
            if (!string.IsNullOrWhiteSpace(textBlock.Text))
            {
                section.Elements.Add(new IngestionDocumentParagraph(textBlock.Text)
                {
                    Text = textBlock.Text,
                });
            }
        }

        return section;
    }
}
