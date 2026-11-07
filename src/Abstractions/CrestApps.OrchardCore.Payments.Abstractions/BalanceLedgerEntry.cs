namespace CrestApps.OrchardCore.Payments;

/// <summary>
/// One change to a stored money balance, such as a gift card's or a customer's store credit: what changed, the balance
/// after it, why, and who or which checkout made it. Each balance keeps its own entry type deriving from this one, so
/// its stored shape stays its own.
/// </summary>
public class BalanceLedgerEntry
{
    /// <summary>
    /// Gets or sets when the balance changed, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets how much the balance actually changed: positive when added, negative when spent.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the balance after the change.
    /// </summary>
    public decimal Balance { get; set; }

    /// <summary>
    /// Gets or sets why the balance changed.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the checkout session the change was made in, if any.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets who made the change, if a person did.
    /// </summary>
    public string Actor { get; set; }
}
