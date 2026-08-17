namespace CrestApps.OrchardCore.Transactions.Models;

/// <summary>
/// The settled state of a <see cref="Transaction"/> ledger entry. The values model the full lifecycle a
/// financial obligation can take, from being recorded through settlement, cancellation, or write-off, so a
/// single provider-agnostic report can always answer "what has not been paid yet".
/// </summary>
public enum TransactionStatus
{
    /// <summary>
    /// The transaction was recorded but is not yet due for collection (for example a scheduled charge).
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The transaction is owed in full and has not been paid. This is the primary "outstanding" state a
    /// customer must settle and an administrator must chase.
    /// </summary>
    Outstanding = 1,

    /// <summary>
    /// Part of the transaction has been paid, but a balance is still outstanding.
    /// </summary>
    PartiallyPaid = 2,

    /// <summary>
    /// The transaction has been paid in full.
    /// </summary>
    Paid = 3,

    /// <summary>
    /// The transaction was canceled before it was paid and is no longer collectable.
    /// </summary>
    Canceled = 4,

    /// <summary>
    /// A collection attempt failed at the provider.
    /// </summary>
    Failed = 5,

    /// <summary>
    /// The transaction was abandoned (for example an unpaid obligation that passed its collection window).
    /// </summary>
    Abandoned = 6,

    /// <summary>
    /// The transaction was paid and later refunded.
    /// </summary>
    Refunded = 7,
}
