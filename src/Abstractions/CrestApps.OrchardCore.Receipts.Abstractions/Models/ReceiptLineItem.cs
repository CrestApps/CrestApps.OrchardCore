namespace CrestApps.OrchardCore.Receipts.Models;

/// <summary>
/// Represents a single billable line printed on a receipt. Amounts are carried as <see cref="decimal"/>,
/// the authoritative representation for financial figures.
/// </summary>
public sealed class ReceiptLineItem
{
    /// <summary>
    /// Gets or sets the human-readable description of what was purchased.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the number of units billed by this line. Defaults to <c>1</c>.
    /// </summary>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Gets or sets the amount charged for a single unit, before tax, in the receipt currency.
    /// </summary>
    public decimal UnitAmount { get; set; }

    /// <summary>
    /// Gets or sets the total amount charged for this line, before tax, in the receipt currency.
    /// </summary>
    public decimal Amount { get; set; }
}
