namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// Credits an issued invoice: drafts a credit note from what is still creditable on it, and issues the credit note, which
/// numbers it and lowers what is owed, refunds, or keeps the amount as the customer's credit, as its disposition says.
/// Each command takes the invoice's lock. The service checks no permission; the caller does.
/// </summary>
public interface ICreditNoteCommandService
{
    /// <summary>
    /// Drafts a credit note for an issued invoice, with each line still creditable on it.
    /// </summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="cancellationToken">A token to cancel the command.</param>
    Task<CreditNoteCommandResult> CreateDraftAsync(string invoiceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Issues a draft credit note. Repeating it once issued returns it with <see cref="CreditNoteCommandResult.AlreadyDone"/>.
    /// </summary>
    /// <param name="creditNoteId">The credit note.</param>
    /// <param name="cancellationToken">A token to cancel the command.</param>
    Task<CreditNoteCommandResult> IssueAsync(string creditNoteId, CancellationToken cancellationToken = default);
}
