using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Sources tax from the customer residence address.
/// </summary>
public sealed class CustomerResidenceTaxSourcingStrategy : ITaxSourcingStrategy
{
    /// <inheritdoc />
    public string Name => TaxSourcingNames.CustomerResidence;

    /// <inheritdoc />
    public Address Resolve(TaxCalculationContext context, ITaxableItem item)
        => context?.Customer?.ResidenceAddress;
}
