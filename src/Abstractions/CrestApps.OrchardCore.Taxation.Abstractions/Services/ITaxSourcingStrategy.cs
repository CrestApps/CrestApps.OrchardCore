using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Determines which address is used to resolve the applicable jurisdictions for a taxable item.
/// Strategies are resolved by <see cref="Name"/>.
/// </summary>
public interface ITaxSourcingStrategy
{
    /// <summary>
    /// Gets the unique name of the sourcing strategy.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Resolves the address that should be used to source tax for the supplied item.
    /// </summary>
    /// <param name="context">The tax calculation context.</param>
    /// <param name="item">The taxable item being sourced.</param>
    /// <returns>The address used for jurisdiction resolution, or <see langword="null"/> when unavailable.</returns>
    Address Resolve(TaxCalculationContext context, ITaxableItem item);
}
