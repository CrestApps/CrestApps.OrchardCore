using CrestApps.OrchardCore.BusinessDocuments.Models;

namespace CrestApps.OrchardCore.BusinessDocuments.Services;

/// <summary>
/// Renders a <see cref="BusinessDocument"/> as a PDF file. It is registered only when the Business Documents PDF
/// feature is enabled, so a consumer that offers a PDF asks for it as an optional service.
/// </summary>
public interface IBusinessDocumentPdfRenderer
{
    /// <summary>
    /// Renders the document.
    /// </summary>
    /// <param name="document">The document to render.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The PDF file.</returns>
    Task<byte[]> RenderAsync(BusinessDocument document, CancellationToken cancellationToken = default);
}
