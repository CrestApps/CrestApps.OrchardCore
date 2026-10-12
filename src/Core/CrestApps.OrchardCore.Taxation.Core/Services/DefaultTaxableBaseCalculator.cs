using System;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;

namespace CrestApps.OrchardCore.Taxation.Core.Services;

/// <summary>
/// Default <see cref="ITaxableBaseCalculator"/> that computes the net taxable base as the line amount
/// (quantity multiplied by unit price) reduced by the line discount.
/// </summary>
public sealed class DefaultTaxableBaseCalculator : ITaxableBaseCalculator
{
    /// <inheritdoc />
    public decimal GetTaxableBase(ITaxableItem item, TaxCalculationContext context)
    {
        ArgumentNullException.ThrowIfNull(item);

        var lineAmount = (item.UnitPrice * item.Quantity) - item.DiscountAmount;

        return lineAmount < 0m ? 0m : lineAmount;
    }
}
