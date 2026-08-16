using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Sources tax from the destination (ship-to) address.
/// </summary>
public sealed class DestinationTaxSourcingStrategy : ITaxSourcingStrategy
{
    /// <inheritdoc />
    public string Name => TaxSourcingNames.Destination;

    /// <inheritdoc />
    public Address Resolve(TaxCalculationContext context, ITaxableItem item)
        => context?.Destination;
}
