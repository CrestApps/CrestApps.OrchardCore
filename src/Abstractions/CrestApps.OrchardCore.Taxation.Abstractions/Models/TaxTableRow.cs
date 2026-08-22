namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// Represents a single row of a <see cref="TaxTable"/>. Rows are used to model brackets, per-unit
/// schedules, and lookup tables. Not all fields apply to every calculation method.
/// </summary>
public sealed class TaxTableRow
{
    /// <summary>
    /// Gets or sets the inclusive lower bound the row applies to.
    /// </summary>
    public decimal Minimum { get; set; }

    /// <summary>
    /// Gets or sets the exclusive upper bound the row applies to. A <see langword="null"/> value means
    /// the row has no upper bound.
    /// </summary>
    public decimal? Maximum { get; set; }

    /// <summary>
    /// Gets or sets the rate applied within the row, expressed as a fraction.
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

    /// <summary>
    /// Creates a copy of the current row.
    /// </summary>
    /// <returns>A new <see cref="TaxTableRow"/> with the same values.</returns>
    public TaxTableRow Clone()
    {
        return new TaxTableRow
        {
            Minimum = Minimum,
            Maximum = Maximum,
            Rate = Rate,
            FixedAmount = FixedAmount,
            BaseAmount = BaseAmount,
        };
    }
}
