using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Sources tax from the customer business address.
/// </summary>
public sealed class CustomerBusinessTaxSourcingStrategy : ITaxSourcingStrategy
{
    /// <inheritdoc />
    public string Name => TaxSourcingNames.CustomerBusiness;

    /// <inheritdoc />
    public Address Resolve(TaxCalculationContext context, ITaxableItem item)
        => context?.Customer?.BusinessAddress;
}
