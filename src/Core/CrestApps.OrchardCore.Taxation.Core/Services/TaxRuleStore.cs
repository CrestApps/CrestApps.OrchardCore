using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Document-backed <see cref="ITaxRuleStore"/> implementation.
/// </summary>
public sealed class TaxRuleStore : NamedCatalog<TaxRule>, ITaxRuleStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TaxRuleStore"/> class.
    /// </summary>
    /// <param name="documentManager">The document manager for the backing document.</param>
    public TaxRuleStore(IDocumentManager<DictionaryDocument<TaxRule>> documentManager)
        : base(documentManager)
    {
    }
}
