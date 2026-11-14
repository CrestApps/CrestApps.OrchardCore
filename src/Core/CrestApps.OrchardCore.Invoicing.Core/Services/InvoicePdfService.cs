using CrestApps.OrchardCore.BusinessDocuments.Services;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// Renders the PDF of an invoice or credit note through the business document PDF renderer, and keeps the PDF of each
/// issued one in <see cref="InvoiceArchiveFileStore"/>, so what was issued is what is downloaded and sent later, whatever
/// the template or the store profile become.
/// </summary>
public sealed class InvoicePdfService : IInvoicePdfService
{
    private readonly IInvoiceDocumentBuilder _builder;
    private readonly IBusinessDocumentPdfRenderer _renderer;
    private readonly InvoiceArchiveFileStore _archive;
    private readonly ILogger _logger;

    public InvoicePdfService(
        IInvoiceDocumentBuilder builder,
        IBusinessDocumentPdfRenderer renderer,
        InvoiceArchiveFileStore archive,
        ILogger<InvoicePdfService> logger)
    {
        _builder = builder;
        _renderer = renderer;
        _archive = archive;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<InvoicePdf> GetAsync(InvoicingDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var fileName = GetFileName(document);

        if (!string.IsNullOrEmpty(document.ArchiveFile))
        {
            try
            {
                if (await _archive.FileStore.GetFileInfoAsync(document.ArchiveFile) is not null)
                {
                    await using var stream = await _archive.FileStore.GetFileStreamAsync(document.ArchiveFile);
                    using var buffer = new MemoryStream();

                    await stream.CopyToAsync(buffer, cancellationToken);

                    return new InvoicePdf(fileName, buffer.ToArray());
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The document can always be rendered again from what it keeps; the archive is the copy, not the record.
                _logger.LogError(ex, "The archived PDF '{File}' of '{DocumentId}' could not be read; it is rendered again.", document.ArchiveFile, document.ItemId);
            }
        }

        return new InvoicePdf(fileName, await RenderAsync(document, cancellationToken));
    }

    /// <inheritdoc/>
    public async Task<bool> ArchiveAsync(InvoicingDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (!document.IssuedUtc.HasValue || !string.IsNullOrEmpty(document.ArchiveFile))
        {
            return false;
        }

        var content = await RenderAsync(document, cancellationToken);
        var kind = document is CreditNote ? "credit-notes" : "invoices";
        var path = $"{kind}/{document.IssuedUtc.Value.Year.ToString(System.Globalization.CultureInfo.InvariantCulture)}/{document.ItemId}.pdf";

        using var stream = new MemoryStream(content);

        // Never replaced: a PDF already stored for this document stays the one that was issued.
        if (await _archive.FileStore.GetFileInfoAsync(path) is null)
        {
            await _archive.FileStore.CreateFileFromStreamAsync(path, stream, overwrite: false);
        }

        document.ArchiveFile = path;

        return true;
    }

    private async Task<byte[]> RenderAsync(InvoicingDocument document, CancellationToken cancellationToken)
        => await _renderer.RenderAsync(await _builder.BuildAsync(document, cancellationToken), cancellationToken);

    private static string GetFileName(InvoicingDocument document)
    {
        var name = string.IsNullOrEmpty(document.Number)
            ? (document is CreditNote ? "draft-credit-note" : "draft-invoice")
            : document.Number;

        return string.Concat(name.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '-' : character)) + ".pdf";
    }
}
