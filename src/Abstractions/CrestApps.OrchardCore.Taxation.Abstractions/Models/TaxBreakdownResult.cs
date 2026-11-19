namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// A document's tax breakdown: one row per treatment, tax code, rate and compound flag, the tax of each item, and the
/// total with its cash rounding.
/// </summary>
public sealed class TaxBreakdownResult
{
    /// <summary>
    /// Gets the breakdown rows: a taxable row per tax and rate, worked out on the summed net, and a row with no tax per
    /// treatment and reason of the items that are not taxed.
    /// </summary>
    public IList<TaxBreakdownRow> Rows { get; } = [];

    /// <summary>
    /// Gets the result of each item, in the order of the request.
    /// </summary>
    public IList<TaxBreakdownItemResult> Items { get; } = [];

    /// <summary>
    /// Gets or sets the tax of the whole document.
    /// </summary>
    public decimal TaxTotal { get; set; }

    /// <summary>
    /// Gets or sets what cash rounding added to (or took off) the total.
    /// </summary>
    public decimal RoundingAmount { get; set; }

    /// <summary>
    /// Gets or sets the document's total: the items' nets plus the tax, plus the cash rounding.
    /// </summary>
    public decimal Total { get; set; }
}
