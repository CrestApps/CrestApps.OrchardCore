namespace CrestApps.OrchardCore.Receipts.Models;

/// <summary>
/// Represents a single tax component printed on a receipt. Each jurisdiction or tax is preserved as its
/// own line so a receipt never collapses multiple taxes into one opaque figure.
/// </summary>
public sealed class ReceiptTaxLine
{
    /// <summary>
    /// Gets or sets the human-readable description of the tax (for example the jurisdiction and rate).
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the tax amount charged for this line, in the receipt currency.
    /// </summary>
    public decimal Amount { get; set; }
}
