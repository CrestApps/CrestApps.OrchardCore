namespace CrestApps.OrchardCore.Invoicing.Models;

/// <summary>
/// What an issued credit note does with the amount it credits.
/// </summary>
public enum CreditNoteDisposition
{
    /// <summary>
    /// Lowers what is still owed on the invoice.
    /// </summary>
    ReduceBalance = 0,

    /// <summary>
    /// Gives the money back through the payment it was paid with.
    /// </summary>
    Refund = 1,

    /// <summary>
    /// Adds the amount to the customer's credit, to use on a later invoice.
    /// </summary>
    CustomerCredit = 2,

    /// <summary>
    /// Records money already given back some other way, such as by refunding the order the invoice bills; the ledger
    /// already has the refund, so nothing more is paid out.
    /// </summary>
    RefundedElsewhere = 3,
}
