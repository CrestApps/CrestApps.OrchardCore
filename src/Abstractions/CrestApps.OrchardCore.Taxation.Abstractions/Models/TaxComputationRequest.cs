namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// The input passed to an <see cref="Services.ITaxCalculationMethod"/> to compute a single tax amount.
/// </summary>
public sealed class TaxComputationRequest
{
    /// <summary>
    /// Gets or sets the taxable base the tax is calculated on. For compound taxes this base already
    /// includes previously calculated taxes.
    /// </summary>
    public decimal TaxableBase { get; set; }

    /// <summary>
    /// Gets or sets the quantity of the taxable item.
    /// </summary>
    public decimal Quantity { get; set; } = 1m;

    /// <summary>
    /// Gets or sets the total weight of the taxable item, when available.
    /// </summary>
    public decimal? Weight { get; set; }

    /// <summary>
    /// Gets or sets the total volume of the taxable item, when available.
    /// </summary>
    public decimal? Volume { get; set; }

    /// <summary>
    /// Gets or sets the rate configured on the rule, expressed as a fraction (for example <c>0.2</c> for 20%).
    /// </summary>
    public decimal? Rate { get; set; }

    /// <summary>
    /// Gets or sets the fixed amount configured on the rule, when applicable.
    /// </summary>
    public decimal? FixedAmount { get; set; }

    /// <summary>
    /// Gets or sets the tax table used by table-driven methods, when applicable.
    /// </summary>
    public TaxTable Table { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the taxable base already includes this tax.
    /// </summary>
    public bool PriceIncludesTax { get; set; }
}
