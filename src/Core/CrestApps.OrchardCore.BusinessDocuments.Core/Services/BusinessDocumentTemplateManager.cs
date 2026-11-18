using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.BusinessDocuments.Services;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.BusinessDocuments.Core.Services;

/// <summary>
/// Manages document templates and picks the template a document is printed with.
/// </summary>
public sealed class BusinessDocumentTemplateManager : CatalogManager<BusinessDocumentTemplate>, IBusinessDocumentTemplateManager
{
    private readonly IBusinessDocumentTemplateStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="BusinessDocumentTemplateManager"/> class.
    /// </summary>
    /// <param name="store">The template store.</param>
    /// <param name="handlers">The template handlers.</param>
    /// <param name="logger">The logger.</param>
    public BusinessDocumentTemplateManager(
        IBusinessDocumentTemplateStore store,
        IEnumerable<ICatalogEntryHandler<BusinessDocumentTemplate>> handlers,
        ILogger<CatalogManager<BusinessDocumentTemplate>> logger)
        : base(store, handlers, logger)
    {
        _store = store;
    }

    /// <inheritdoc />
    public async Task<BusinessDocumentTemplate> GetForKindAsync(string kind, string templateId = null, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(templateId) &&
            await FindByIdAsync(templateId, cancellationToken) is { } named &&
            named.AppliesTo(kind))
        {
            return named;
        }

        var defaults = await _store.GetDefaultsAsync(cancellationToken);

        // A template made for the kind wins over one made for every kind; among equals, the one changed last wins.
        return defaults
            .Where(template => template.IsDefault && template.AppliesTo(kind))
            .OrderByDescending(template => template.Kinds is { Count: > 0 })
            .ThenByDescending(template => template.ModifiedUtc ?? template.CreatedUtc)
            .ThenBy(template => template.ItemId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task ApplyAsync(BusinessDocument document, string templateId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var template = await GetForKindAsync(document.Kind, templateId, cancellationToken);

        if (template is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(document.LogoUrl))
        {
            document.LogoUrl = template.LogoUrl;
        }

        if (string.IsNullOrWhiteSpace(document.AccentColor))
        {
            document.AccentColor = template.AccentColor;
        }

        if (document.PaperSize == BusinessDocumentPaperSize.Default)
        {
            document.PaperSize = template.PaperSize;
        }

        if (string.IsNullOrWhiteSpace(document.Footer))
        {
            document.Footer = template.Footer;
        }
    }
}
