namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// What a document's tax breakdown came to for one item.
/// </summary>
public sealed class TaxBreakdownItemResult
{
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the item's net after the document's discounts.
    /// </summary>
    public decimal Net { get; set; }

    /// <summary>
    /// Gets or sets the item's net before the document's discounts.
    /// </summary>
    public decimal BaseNet { get; set; }

    /// <summary>
    /// Gets or sets the item's share of the document's tax.
    /// </summary>
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// Gets or sets how tax applied to the item in the end.
    /// </summary>
    public TaxTreatment Treatment { get; set; }

    /// <summary>
    /// Gets or sets why the item was not taxed, for a treatment that is not taxable.
    /// </summary>
    public string TreatmentReason { get; set; }
}
