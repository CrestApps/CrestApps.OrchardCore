using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Core.Indexes;

/// <summary>
/// The queryable projection of a <see cref="Transaction"/> ledger entry.
/// </summary>
public sealed class TransactionIndex : CatalogItemIndex
{
    /// <summary>
    /// The transaction title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// The origin/provider key that created the transaction.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// The owner of the obligation.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// The reference type the transaction is for.
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// The reference id the transaction is for.
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// The originating checkout session id.
    /// </summary>
    public string CheckoutSessionId { get; set; }

    /// <summary>
    /// The obligation id within the originating checkout.
    /// </summary>
    public string ObligationId { get; set; }

    /// <summary>
    /// The ISO-4217 currency code.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// The total amount owed including tax.
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// The amount paid so far.
    /// </summary>
    public decimal AmountPaid { get; set; }

    /// <summary>
    /// The current lifecycle state of the transaction.
    /// </summary>
    public TransactionStatus Status { get; set; }

    /// <summary>
    /// The UTC time the transaction is due, when applicable.
    /// </summary>
    public DateTime? DueUtc { get; set; }

    /// <summary>
    /// The UTC time the transaction was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// The UTC time the transaction was last updated.
    /// </summary>
    public DateTime UpdatedUtc { get; set; }
}
