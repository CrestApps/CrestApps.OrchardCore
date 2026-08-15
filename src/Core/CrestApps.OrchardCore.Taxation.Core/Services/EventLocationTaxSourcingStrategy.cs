using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Sources tax from the location where an event takes place.
/// </summary>
public sealed class EventLocationTaxSourcingStrategy : ITaxSourcingStrategy
{
    /// <inheritdoc />
    public string Name => TaxSourcingNames.EventLocation;

    /// <inheritdoc />
    public TaxAddress Resolve(TaxCalculationContext context, ITaxableItem item)
        => context?.EventLocation;
}
