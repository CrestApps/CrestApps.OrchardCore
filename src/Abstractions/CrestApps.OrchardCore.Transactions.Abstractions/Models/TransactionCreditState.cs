namespace CrestApps.OrchardCore.Transactions.Models;

/// <summary>
/// Where the customer credit change a timeline entry stands for is (<see cref="TransactionEvent.CreditChange"/>). The
/// entry is saved first, as the intent, and the credit is changed after it with the entry's id as the reference, so a
/// process that stops between the two leaves an entry that is completed later, never a credit change without its entry.
/// </summary>
public enum TransactionCreditState
{
    /// <summary>
    /// The entry changes no credit.
    /// </summary>
    None = 0,

    /// <summary>
    /// The credit change is still to be made.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// The credit was changed.
    /// </summary>
    Settled = 2,

    /// <summary>
    /// The credit could not be changed; a payment issue says so for someone to handle.
    /// </summary>
    Failed = 3,
}
