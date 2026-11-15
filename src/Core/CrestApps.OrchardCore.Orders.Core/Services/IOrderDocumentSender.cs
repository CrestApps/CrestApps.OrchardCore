using CrestApps.OrchardCore.Orders.Core.Models;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// Sends a draft order the store put together to its buyer in place of the order's own invoice or quote e-mail, such as
/// an invoicing module that issues an invoice (or an estimate, for a quote) from the order. The order keeps its own pay
/// link and checkout: whatever is sent must send the buyer there to pay. The first sender that handles an order sends it;
/// with none, the order's own e-mail is sent.
/// </summary>
public interface IOrderDocumentSender
{
    /// <summary>
    /// Sends <paramref name="order"/> to its buyer, as an invoice, or as a quote while it has one
    /// (<see cref="Order.Quote"/>). Returns <see langword="null"/> when this sender does not send it, so the next one or
    /// the order's own e-mail does.
    /// </summary>
    /// <param name="order">The draft order, with its invoice details (<see cref="Order.Invoice"/>).</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<OrderDocumentSendResult> SendAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the address of the invoice this sender made for <paramref name="order"/>, which the order's own invoice
    /// links open instead of the order's printable invoice, or <see langword="null"/> when it made none.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <param name="forStaff">Whether the link is for staff (the invoice's admin page) rather than the buyer.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <remarks>It returns <see langword="null"/> by default.</remarks>
    Task<string> GetInvoiceUrlAsync(Order order, bool forStaff, CancellationToken cancellationToken = default)
        => Task.FromResult<string>(null);
}

/// <summary>
/// Finds the invoice another module made for an order (<see cref="IOrderDocumentSender.GetInvoiceUrlAsync"/>).
/// </summary>
public static class OrderDocumentLinks
{
    /// <summary>
    /// Returns the address of the first invoice a sender made for the order, or <see langword="null"/>.
    /// </summary>
    /// <param name="senders">The order document senders.</param>
    /// <param name="order">The order.</param>
    /// <param name="forStaff">Whether the link is for staff rather than the buyer.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public static async Task<string> GetInvoiceUrlAsync(IEnumerable<IOrderDocumentSender> senders, Order order, bool forStaff, CancellationToken cancellationToken = default)
    {
        foreach (var sender in senders ?? [])
        {
            if (await sender.GetInvoiceUrlAsync(order, forStaff, cancellationToken) is { Length: > 0 } url)
            {
                return url;
            }
        }

        return null;
    }
}

/// <summary>
/// What sending a draft order as another module's document came to.
/// </summary>
public sealed class OrderDocumentSendResult
{
    /// <summary>
    /// Gets a value indicating whether the order was sent, so its invoice and quote are marked sent.
    /// </summary>
    public bool Sent
        => Errors.Count == 0;

    /// <summary>
    /// Gets or sets the note added to the order, such as which invoice it was sent as; <see langword="null"/> adds the
    /// order's usual note.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets the message shown to staff, such as the number of the invoice it was sent as.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets whether the document was e-mailed to the buyer. When it was only made (e-mail isn't set up), the
    /// order says so, and staff share the document's link instead.
    /// </summary>
    public bool Emailed { get; set; }

    /// <summary>
    /// Gets or sets the number of the document the order was sent as, such as an invoice or estimate number.
    /// </summary>
    public string DocumentNumber { get; set; }

    /// <summary>
    /// Gets the reasons the order was not sent.
    /// </summary>
    public IList<string> Errors { get; } = [];

    /// <summary>
    /// Adds a reason and returns the result.
    /// </summary>
    /// <param name="error">Why the order was not sent.</param>
    public OrderDocumentSendResult Fail(string error)
    {
        Errors.Add(error);

        return this;
    }
}
