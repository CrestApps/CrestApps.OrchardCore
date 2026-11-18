using CrestApps.OrchardCore.Invoicing.Models;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// Sends an invoice to its customer by e-mail with its PDF attached, and records the delivery on the invoice.
/// </summary>
public interface IInvoiceSender
{
    /// <summary>
    /// Sends an invoice. A draft is issued first when the invoicing settings say sending issues it; otherwise only issued
    /// invoices are sent.
    /// </summary>
    /// <param name="request">The invoice and who it goes to.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    Task<InvoiceSendResult> SendAsync(InvoiceSendRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Who an invoice is sent to.
/// </summary>
public sealed class InvoiceSendRequest
{
    /// <summary>
    /// Gets or sets the invoice.
    /// </summary>
    public string InvoiceId { get; set; }

    /// <summary>
    /// Gets or sets the address it is sent to; the bill-to's e-mail when empty.
    /// </summary>
    public string To { get; set; }

    /// <summary>
    /// Gets or sets the addresses copied; the bill-to's copy addresses when empty.
    /// </summary>
    public IList<string> Cc { get; set; } = [];

    /// <summary>
    /// Gets or sets the addresses blind-copied.
    /// </summary>
    public IList<string> Bcc { get; set; } = [];
}

/// <summary>
/// What sending an invoice did.
/// </summary>
public sealed class InvoiceSendResult
{
    /// <summary>
    /// Gets whether it was sent.
    /// </summary>
    public bool Succeeded
        => Errors.Count == 0;

    /// <summary>
    /// Gets or sets the invoice as it is after sending.
    /// </summary>
    public Invoice Invoice { get; set; }

    /// <summary>
    /// Gets or sets whether the draft was issued to send it.
    /// </summary>
    public bool Issued { get; set; }

    /// <summary>
    /// Gets why it was not sent.
    /// </summary>
    public IList<string> Errors { get; } = [];
}
