using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessDocuments.Models;

namespace CrestApps.OrchardCore.BusinessDocuments.Services;

/// <summary>
/// Manages the document templates, so their handlers run, and picks the template a document is printed with.
/// </summary>
public interface IBusinessDocumentTemplateManager : ICatalogManager<BusinessDocumentTemplate>
{
    /// <summary>
    /// Gets the template a document of a kind is printed with.
    /// </summary>
    /// <param name="kind">The kind of document, such as <c>Invoice</c>.</param>
    /// <param name="templateId">The id of the template the document names, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>
    /// The named template when it exists and is for the kind; otherwise the default template for the kind, a template
    /// that lists the kind before one for every kind; otherwise <see langword="null"/>.
    /// </returns>
    Task<BusinessDocumentTemplate> GetForKindAsync(string kind, string templateId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies the logo, the accent color, the paper size and the footer of the template the document is printed with
    /// onto the document, where the document has none of its own.
    /// </summary>
    /// <param name="document">The document.</param>
    /// <param name="templateId">The id of the template the document names, or <see langword="null"/> for the default
    /// template of its kind.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the template is applied.</returns>
    Task ApplyAsync(BusinessDocument document, string templateId = null, CancellationToken cancellationToken = default);
}
