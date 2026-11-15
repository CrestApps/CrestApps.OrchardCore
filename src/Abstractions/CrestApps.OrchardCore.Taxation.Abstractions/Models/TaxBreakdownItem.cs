namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// One item of a document's tax breakdown: a line or a charge.
/// </summary>
public sealed class TaxBreakdownItem
{
    /// <summary>
    /// Gets or sets the item's identifier, returned with its result.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the item's amount after the document's discounts, rounded to the currency: net, or gross when the
    /// document's prices include tax.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the item's amount before the document's discounts, in the same terms as <see cref="Amount"/>, so the
    /// net the item had before them is reported; the same as <see cref="Amount"/> for an item they don't touch.
    /// </summary>
    public decimal BaseAmount { get; set; }

    /// <summary>
    /// Gets or sets whether the item is a charge (shipping, a surcharge) rather than a line: the document's discounts
    /// never touch it, so its net before them is its net.
    /// </summary>
    public bool IsCharge { get; set; }

    /// <summary>
    /// Gets or sets how tax applies to the item, or <see langword="null"/> for the document's treatment.
    /// </summary>
    public TaxTreatment? Treatment { get; set; }

    /// <summary>
    /// Gets or sets why the item is not taxed, for a treatment that is not taxable.
    /// </summary>
    public string TreatmentReason { get; set; }

    /// <summary>
    /// Gets the taxes that apply to the item, as the tax rules found them.
    /// </summary>
    public IList<TaxBreakdownRate> Taxes { get; } = [];
}
