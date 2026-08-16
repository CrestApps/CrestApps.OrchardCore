using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Sources tax from the location where a service is performed.
/// </summary>
public sealed class ServiceLocationTaxSourcingStrategy : ITaxSourcingStrategy
{
    /// <inheritdoc />
    public string Name => TaxSourcingNames.ServiceLocation;

    /// <inheritdoc />
    public Address Resolve(TaxCalculationContext context, ITaxableItem item)
        => context?.ServiceLocation;
}
