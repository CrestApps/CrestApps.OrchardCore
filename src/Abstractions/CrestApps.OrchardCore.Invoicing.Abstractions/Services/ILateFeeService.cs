using CrestApps.Core.Services;
using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// Manages the late-fee policies.
/// </summary>
public interface ILateFeePolicyManager : ICatalogManager<LateFeePolicy>
{
    /// <summary>
    /// Gets the policies that charge fees, by name.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IReadOnlyList<LateFeePolicy>> GetEnabledAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Charges late fees on overdue invoices as debit notes (an <see cref="Invoice"/> of kind
/// <see cref="InvoiceKind.DebitNote"/> whose <see cref="Invoice.RelatedInvoiceId"/> names the invoice), once per
/// policy step: the debit note's source is <c>LateFee</c>, <c>{invoiceId}:{policyId}:{step}</c>, so a step is never
/// charged twice. The balance is read from the ledger right before each fee.
/// </summary>
public interface ILateFeeService
{
    /// <summary>
    /// Charges the next fee due on each overdue invoice, at most one per invoice and policy. Returns how many debit notes
    /// were issued.
    /// </summary>
    /// <param name="today">The site's date.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<int> AssessDueAsync(DateOnly today, CancellationToken cancellationToken = default);

    /// <summary>
    /// Charges the next fee due on one invoice, under each enabled policy.
    /// </summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="today">The site's date.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<LateFeeResult> AssessAsync(string invoiceId, DateOnly today, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the debit notes charged on an invoice, oldest first.
    /// </summary>
    /// <param name="invoiceId">The invoice.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IReadOnlyList<Invoice>> ListDebitNotesAsync(string invoiceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// What assessing late fees on an invoice came to.
/// </summary>
public sealed class LateFeeResult
{
    /// <summary>
    /// Gets the debit notes issued.
    /// </summary>
    public IList<Invoice> DebitNotes { get; } = [];

    /// <summary>
    /// Gets why a fee that was due could not be charged.
    /// </summary>
    public IList<string> Errors { get; } = [];
}
