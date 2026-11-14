namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// The commands that change an invoice after it is drafted: issue it, void it, write it off and reinstate it.
/// </summary>
/// <remarks>
/// Each command takes a distributed lock on the invoice, loads it and the transaction it posted fresh, decides from what
/// the ledger holds (never from the amounts mirrored on the invoice), and is idempotent: repeating a command that
/// completed returns the same invoice with <see cref="InvoiceCommandResult.AlreadyDone"/>. A refused command returns its
/// reasons in <see cref="InvoiceCommandResult.Errors"/> rather than throwing. The service does not check permissions.
/// </remarks>
public interface IInvoiceCommandService
{
    /// <summary>
    /// Issues a draft: validates it, calculates it a final time, copies the issuer, the bill-to's billing profile, the
    /// payment term and the effective settings onto it, numbers it, posts its transaction (none for a zero total, which is
    /// paid at once) and opens it, in one unit of work, so a refused or failed issue takes no number and posts nothing.
    /// </summary>
    /// <param name="invoiceId">The invoice identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<InvoiceCommandResult> IssueAsync(string invoiceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Voids an open invoice nothing was paid toward and no credit note was issued for: cancels its transaction (refused
    /// when a payment is found there, under the transaction's lock) and keeps its number. Otherwise the invoice is
    /// corrected with a credit note.
    /// </summary>
    /// <param name="invoiceId">The invoice identifier.</param>
    /// <param name="reason">Why it is voided; required.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<InvoiceCommandResult> VoidAsync(string invoiceId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes off what is still owed on an open invoice as uncollectible, with a write-off adjustment on its transaction
    /// of the amount the transaction still owes.
    /// </summary>
    /// <param name="invoiceId">The invoice identifier.</param>
    /// <param name="reason">Why it is written off; required.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<InvoiceCommandResult> WriteOffAsync(string invoiceId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reinstates an uncollectible invoice: takes back its write-off adjustment, so what it wrote off is owed again.
    /// </summary>
    /// <param name="invoiceId">The invoice identifier.</param>
    /// <param name="reason">Why it is reinstated; required.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<InvoiceCommandResult> ReinstateAsync(string invoiceId, string reason, CancellationToken cancellationToken = default);
}
