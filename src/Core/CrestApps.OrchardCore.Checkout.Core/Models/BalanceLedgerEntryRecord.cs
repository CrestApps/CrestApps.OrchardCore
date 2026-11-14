namespace CrestApps.OrchardCore.Checkout.Core.Models;

/// <summary>
/// An older change to a stored balance, such as a gift card's or store credit's, moved out of the balance's own document
/// so the document stays small: the balance keeps its latest changes and the rest live here, one record each, indexed by
/// balance and by reference.
/// </summary>
public sealed class BalanceLedgerEntryRecord
{
    /// <summary>
    /// Gets or sets the document id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the kind of balance, such as <c>StoreCredit</c> or <c>GiftCard</c>.
    /// </summary>
    public string Ledger { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the balance's document.
    /// </summary>
    public string AccountId { get; set; }

    /// <summary>
    /// Gets or sets when the balance changed, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets how much the balance changed.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the balance after the change.
    /// </summary>
    public decimal Balance { get; set; }

    /// <summary>
    /// Gets or sets why it changed.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the checkout session it changed in, if any.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets who changed it, if a person did.
    /// </summary>
    public string Actor { get; set; }

    /// <summary>
    /// Gets or sets what the change was made for, when the balance records one, such as a transaction's payment.
    /// </summary>
    public string Reference { get; set; }
}
