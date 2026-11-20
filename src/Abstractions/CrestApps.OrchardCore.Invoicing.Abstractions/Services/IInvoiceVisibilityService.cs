using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// Decides which invoices a signed-in user sees as their own: the issued invoices billed to a party they have billing
/// access to (themselves, their customer record, or an account they are an admin of). A draft is never a customer's to
/// see.
/// </summary>
public interface IInvoiceVisibilityService
{
    /// <summary>
    /// Whether the user may see the invoice.
    /// </summary>
    /// <param name="invoice">The invoice.</param>
    /// <param name="userId">The signed-in user's id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    Task<bool> CanViewAsync(Invoice invoice, string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The query of the invoices the user may see.
    /// </summary>
    /// <param name="userId">The signed-in user's id.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    Task<InvoiceQuery> CreateQueryAsync(string userId, CancellationToken cancellationToken = default);
}
