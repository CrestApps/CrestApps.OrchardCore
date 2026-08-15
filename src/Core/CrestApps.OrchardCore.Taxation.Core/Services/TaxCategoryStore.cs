using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Document-backed <see cref="ITaxCategoryStore"/> implementation.
/// </summary>
public sealed class TaxCategoryStore : NamedCatalog<TaxCategory>, ITaxCategoryStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TaxCategoryStore"/> class.
    /// </summary>
    /// <param name="documentManager">The document manager for the backing document.</param>
    public TaxCategoryStore(IDocumentManager<DictionaryDocument<TaxCategory>> documentManager)
        : base(documentManager)
    {
    }
}
