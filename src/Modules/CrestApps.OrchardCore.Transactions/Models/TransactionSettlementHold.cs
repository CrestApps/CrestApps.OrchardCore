namespace CrestApps.OrchardCore.Transactions.Models;

/// <summary>
/// The checkout whose payment for a transaction began last, kept on the transaction. While that payment may still take
/// the money, another checkout of the same transaction (a second tab of its pay link) isn't let through to pay it too.
/// </summary>
public sealed class TransactionSettlementHold
{
    /// <summary>
    /// Gets or sets the checkout session whose payment began.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets when its payment began.
    /// </summary>
    public DateTime HeldUtc { get; set; }
}
