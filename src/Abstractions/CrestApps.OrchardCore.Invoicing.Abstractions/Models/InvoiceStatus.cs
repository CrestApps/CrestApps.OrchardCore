namespace CrestApps.OrchardCore.Invoicing.Models;

/// <summary>
/// The life cycle of an invoice. How much is paid is <see cref="InvoiceSettlement"/>, whether it was sent is
/// <see cref="InvoiceDelivery"/>, and overdue is worked out from the due date; none of them is a status.
/// </summary>
public enum InvoiceStatus
{
    /// <summary>
    /// Being written: no number, no transaction, and it can be changed or deleted.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Waiting for someone to approve it before it is issued.
    /// </summary>
    PendingApproval = 1,

    /// <summary>
    /// Issued: numbered, locked and posted to the ledger.
    /// </summary>
    Open = 2,

    /// <summary>
    /// Voided: it keeps its number but asks for nothing. Final.
    /// </summary>
    Void = 3,

    /// <summary>
    /// Written off as uncollectible. It can be reinstated.
    /// </summary>
    Uncollectible = 4,
}
