namespace CrestApps.OrchardCore.Taxation.ViewModels;

/// <summary>
/// Represents a single editable row of a tax table.
/// </summary>
public class TaxTableRowViewModel
{
    /// <summary>
    /// Gets or sets the inclusive lower bound the row applies to.
    /// </summary>
    public decimal Minimum { get; set; }

    /// <summary>
    /// Gets or sets the exclusive upper bound the row applies to. An empty value means the row has no
    /// upper bound.
    /// </summary>
    public decimal? Maximum { get; set; }

    /// <summary>
    /// Gets or sets the rate applied within the row, expressed as a fraction (for example 0.2 for 20%).
    /// </summary>
    public decimal Rate { get; set; }

    /// <summary>
    /// Gets or sets a fixed amount applied within the row.
    /// </summary>
    public decimal FixedAmount { get; set; }

    /// <summary>
    /// Gets or sets a base amount that is added before the rate is applied, used by tiered schedules.
    /// </summary>
    public decimal BaseAmount { get; set; }
}
