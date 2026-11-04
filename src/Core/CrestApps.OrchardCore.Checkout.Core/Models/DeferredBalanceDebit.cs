namespace CrestApps.OrchardCore.Checkout.Core.Models;

/// <summary>
/// What a deferred balance tender puts on the invoice it opens for the part of a checkout that is owed later: its title,
/// source, due date and first event, and the account it belongs to, if any.
/// </summary>
public sealed class DeferredBalanceDebit
{
    /// <summary>
    /// Gets or sets the invoice's title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the invoice's transaction source.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets when the balance is due, in UTC.
    /// </summary>
    public DateTime DueUtc { get; set; }

    /// <summary>
    /// Gets or sets the message of the invoice's created event.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the kind of record the invoice belongs to besides the checkout, such as an account, if any.
    /// </summary>
    public string ParentReferenceType { get; set; }

    /// <summary>
    /// Gets or sets the ID of the record the invoice belongs to besides the checkout, if any.
    /// </summary>
    public string ParentReferenceId { get; set; }

    /// <summary>
    /// Gets or sets what the tender read while preparing the debit and needs again after the invoice is made, such as
    /// the order.
    /// </summary>
    public object State { get; set; }
}
