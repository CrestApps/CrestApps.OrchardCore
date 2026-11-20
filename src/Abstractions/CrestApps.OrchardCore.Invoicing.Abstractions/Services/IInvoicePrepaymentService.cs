using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// Deposits invoiced before the work: a prepayment invoice (<see cref="InvoiceKind.Prepayment"/>) asks for part of a
/// draft invoice's total first, and the final invoice deducts what was paid on it as a <see cref="DocumentLineKind.Deposit"/>
/// line, once.
/// </summary>
public interface IInvoicePrepaymentService
{
    /// <summary>
    /// Makes a draft prepayment invoice for a percent or an amount of a draft invoice's total, linked to it
    /// (<see cref="Invoice.RelatedInvoiceId"/>), billed to the same customer.
    /// </summary>
    /// <param name="invoiceId">The draft invoice the deposit is for.</param>
    /// <param name="percent">The percent of its total asked first, or <see langword="null"/>.</param>
    /// <param name="amount">The amount asked first, used when no percent is given.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<InvoiceCommandResult> RequestDepositAsync(string invoiceId, decimal? percent, decimal? amount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the prepayment invoices a draft invoice can deduct: issued, paid at least in part, billed to the same
    /// customer (or asked from this invoice), and not deducted by any invoice that is not void.
    /// </summary>
    /// <param name="invoiceId">The draft invoice.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IReadOnlyList<Invoice>> ListDeductibleAsync(string invoiceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deducts what was paid on a prepayment invoice from a draft invoice, as a <see cref="DocumentLineKind.Deposit"/>
    /// line of a negative price, at most the draft's total. Refused when it was deducted already.
    /// </summary>
    /// <param name="invoiceId">The draft invoice.</param>
    /// <param name="prepaymentInvoiceId">The prepayment invoice.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<InvoiceCommandResult> DeductAsync(string invoiceId, string prepaymentInvoiceId, CancellationToken cancellationToken = default);
}
