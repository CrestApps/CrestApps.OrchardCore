using System.Collections.Generic;
using System.Linq;

namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// Represents the detailed outcome of a tax calculation.
/// </summary>
public sealed class TaxCalculationResult
{
    /// <summary>
    /// Gets or sets the currency the amounts are expressed in.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the total taxable base across all lines.
    /// </summary>
    public decimal TaxableAmount { get; set; }

    /// <summary>
    /// Gets or sets the total tax across all lines.
    /// </summary>
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// Gets or sets the total amount, including tax that is not already included in the price.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Gets or sets the individual tax lines that explain the determination.
    /// </summary>
    public IList<TaxLine> Lines { get; set; } = [];

    /// <summary>
    /// Gets the tax lines that apply to the specified taxable item.
    /// </summary>
    /// <param name="itemId">The identifier of the taxable item.</param>
    /// <returns>The tax lines produced for the item.</returns>
    public IEnumerable<TaxLine> GetLinesForItem(string itemId)
        => Lines.Where(line => line.ItemId == itemId);
}
