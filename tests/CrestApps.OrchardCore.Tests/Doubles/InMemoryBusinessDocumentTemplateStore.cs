using CrestApps.Core.Services;
using CrestApps.OrchardCore.BusinessDocuments.Core.Services;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Doubles;

/// <summary>
/// A document template store kept in memory.
/// </summary>
internal sealed class InMemoryBusinessDocumentTemplateStore : InMemoryCatalog<BusinessDocumentTemplate>, IBusinessDocumentTemplateStore
{
    public Task<IReadOnlyList<BusinessDocumentTemplate>> GetDefaultsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<BusinessDocumentTemplate>>(Entries.Where(template => template.IsDefault).ToList());

    /// <summary>
    /// Creates a template manager over the store.
    /// </summary>
    /// <param name="handlers">The template handlers the manager runs.</param>
    public BusinessDocumentTemplateManager CreateManager(params ICatalogEntryHandler<BusinessDocumentTemplate>[] handlers)
        => new(this, handlers, NullLogger<CatalogManager<BusinessDocumentTemplate>>.Instance);
}
