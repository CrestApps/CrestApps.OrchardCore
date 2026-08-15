using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Document-backed <see cref="ITaxJurisdictionStore"/> implementation.
/// </summary>
public sealed class TaxJurisdictionStore : NamedCatalog<TaxJurisdiction>, ITaxJurisdictionStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TaxJurisdictionStore"/> class.
    /// </summary>
    /// <param name="documentManager">The document manager for the backing document.</param>
    public TaxJurisdictionStore(IDocumentManager<DictionaryDocument<TaxJurisdiction>> documentManager)
        : base(documentManager)
    {
    }
}
