using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// The PDF of an invoice or credit note: the one stored when it was issued, or for a draft one made on the spot.
/// </summary>
public interface IInvoicePdfService
{
    /// <summary>
    /// Gets the PDF of a document: the archived one once issued, so it is always what was issued, else a fresh rendering.
    /// </summary>
    /// <param name="document">The invoice or credit note.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    Task<InvoicePdf> GetAsync(InvoicingDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renders an issued document and stores the PDF, setting <see cref="InvoicingDocument.ArchiveFile"/>. A document
    /// already archived keeps its first PDF. Returns whether a PDF was stored now.
    /// </summary>
    /// <param name="document">The issued invoice or credit note.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    Task<bool> ArchiveAsync(InvoicingDocument document, CancellationToken cancellationToken = default);
}

/// <summary>
/// The PDF of an invoice or credit note.
/// </summary>
/// <param name="FileName">The file name to download it as.</param>
/// <param name="Content">The PDF.</param>
public sealed record InvoicePdf(string FileName, byte[] Content);
