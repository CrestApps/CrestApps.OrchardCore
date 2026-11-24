using CrestApps.OrchardCore.BusinessDocuments.Models;

namespace CrestApps.OrchardCore.BusinessDocuments.Services;

/// <summary>
/// Renders a <see cref="BusinessDocument"/> as a self-contained HTML fragment for the body of an e-mail: tables and
/// inline styles, no stylesheet and no script, which mail clients render reliably. A page in the browser shows the
/// <c>BusinessDocument</c> shape instead.
/// </summary>
public interface IBusinessDocumentHtmlRenderer
{
    /// <summary>
    /// Renders the document. Every value taken from the document is HTML-encoded.
    /// </summary>
    /// <param name="document">The document to render.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The HTML fragment.</returns>
    Task<string> RenderAsync(BusinessDocument document, CancellationToken cancellationToken = default);
}
