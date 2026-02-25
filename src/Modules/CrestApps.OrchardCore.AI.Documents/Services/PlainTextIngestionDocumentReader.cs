using System.Text;
using Microsoft.Extensions.DataIngestion;

namespace CrestApps.OrchardCore.AI.Documents.Services;

/// <summary>
/// Reads plain text files (txt, csv, json, xml, html, log, yaml, yml) into an
/// <see cref="IngestionDocument"/> for downstream normalization and chunking.
/// </summary>
internal sealed class PlainTextIngestionDocumentReader : IngestionDocumentReader
{
    public override async Task<IngestionDocument> ReadAsync(
        Stream source,
        string identifier,
        string mediaType,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var reader = new StreamReader(source, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var text = await reader.ReadToEndAsync(cancellationToken);

        var document = new IngestionDocument(identifier);

        if (!string.IsNullOrWhiteSpace(text))
        {
            var section = new IngestionDocumentSection();
            section.Elements.Add(new IngestionDocumentParagraph(text)
            {
                Text = text,
            });

            document.Sections.Add(section);
        }

        return document;
    }
}
