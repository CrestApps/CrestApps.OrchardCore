using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// Signed, expiring links that show an invoice to its customer without signing in.
/// </summary>
public interface IInvoiceShareLinkService
{
    /// <summary>
    /// Creates the link to the invoice's public page, valid for the days the invoicing settings say.
    /// </summary>
    /// <param name="invoice">The invoice.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    Task<string> CreateUrlAsync(Invoice invoice, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the invoice id from a link's token, or <see langword="null"/> when it is not valid or has expired.
    /// </summary>
    /// <param name="token">The token from the link.</param>
    string GetInvoiceId(string token);
}
