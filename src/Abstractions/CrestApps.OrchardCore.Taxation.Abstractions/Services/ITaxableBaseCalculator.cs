using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Taxation.Services;

/// <summary>
/// Computes the taxable base for a taxable item, taking discounts and other charges into account.
/// </summary>
public interface ITaxableBaseCalculator
{
    /// <summary>
    /// Computes the net taxable base for the supplied item, before any tax is applied.
    /// </summary>
    /// <param name="item">The taxable item.</param>
    /// <param name="context">The tax calculation context.</param>
    /// <returns>The net taxable base.</returns>
    decimal GetTaxableBase(ITaxableItem item, TaxCalculationContext context);
}
