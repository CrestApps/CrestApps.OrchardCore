using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Models;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Document-backed <see cref="IMerchantTaxRegistrationStore"/> implementation.
/// </summary>
public sealed class MerchantTaxRegistrationStore : Catalog<MerchantTaxRegistration>, IMerchantTaxRegistrationStore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MerchantTaxRegistrationStore"/> class.
    /// </summary>
    /// <param name="documentManager">The document manager for the backing document.</param>
    public MerchantTaxRegistrationStore(IDocumentManager<DictionaryDocument<MerchantTaxRegistration>> documentManager)
        : base(documentManager)
    {
    }
}
