namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// One row of a document's tax breakdown.
/// </summary>
public sealed class TaxBreakdownRow
{
    public TaxTreatment Treatment { get; set; }

    public string Code { get; set; }

    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the rate in percent; zero for a row of items that are not taxed.
    /// </summary>
    public decimal Rate { get; set; }

    public bool IsCompound { get; set; }

    public decimal TaxableAmount { get; set; }

    public decimal TaxAmount { get; set; }

    /// <summary>
    /// Gets or sets why the row's items are not taxed, for a treatment that is not taxable.
    /// </summary>
    public string ExemptionReason { get; set; }
}
