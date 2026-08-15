using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Document-backed <see cref="ITaxTableStore"/> implementation.
/// </summary>
public sealed class TaxTableStore : NamedCatalog<TaxTable>, ITaxTableStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TaxTableStore"/> class.
    /// </summary>
    /// <param name="documentManager">The document manager for the backing document.</param>
    public TaxTableStore(IDocumentManager<DictionaryDocument<TaxTable>> documentManager)
        : base(documentManager)
    {
    }
}
