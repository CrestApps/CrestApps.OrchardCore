using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Sources tax from the origin (ship-from) address. The item origin takes precedence over the
/// transaction origin.
/// </summary>
public sealed class OriginTaxSourcingStrategy : ITaxSourcingStrategy
{
    /// <inheritdoc />
    public string Name => TaxSourcingNames.Origin;

    /// <inheritdoc />
    public TaxAddress Resolve(TaxCalculationContext context, ITaxableItem item)
        => item?.Origin ?? context?.Origin;
}
