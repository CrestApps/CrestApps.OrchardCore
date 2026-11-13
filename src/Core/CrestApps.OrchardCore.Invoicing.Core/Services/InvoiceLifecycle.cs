using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.StateMachines;

namespace CrestApps.OrchardCore.Invoicing.Core.Services;

/// <summary>
/// The rules of an invoice's life cycle: which status follows which, and what a document in a status still allows. The
/// invoice manager enforces them; the admin pages only read them to offer the actions that would work.
/// </summary>
public static class InvoiceLifecycle
{
    private static readonly TransitionTable<InvoiceStatus> _transitions = new(new Dictionary<InvoiceStatus, InvoiceStatus[]>
    {
        [InvoiceStatus.Draft] = [InvoiceStatus.Open, InvoiceStatus.PendingApproval],
        [InvoiceStatus.PendingApproval] = [InvoiceStatus.Open, InvoiceStatus.Draft],
        [InvoiceStatus.Open] = [InvoiceStatus.Void, InvoiceStatus.Uncollectible],
        [InvoiceStatus.Uncollectible] = [InvoiceStatus.Open],
        [InvoiceStatus.Void] = [],
    });

    /// <summary>
    /// Returns whether an invoice may move from one status to another.
    /// </summary>
    /// <param name="from">The current status.</param>
    /// <param name="to">The status asked for.</param>
    /// <returns><see langword="true"/> when the move is allowed.</returns>
    public static bool CanMove(InvoiceStatus from, InvoiceStatus to)
        => _transitions.CanMove(from, to);

    /// <summary>
    /// Returns whether no status follows the given one.
    /// </summary>
    /// <param name="status">The status.</param>
    /// <returns><see langword="true"/> for a final status.</returns>
    public static bool IsTerminal(InvoiceStatus status)
        => _transitions.IsTerminal(status);

    /// <summary>
    /// Returns whether the lines, parties, dates and amounts of an invoice may still be changed: only while it is a draft.
    /// </summary>
    /// <param name="invoice">The invoice.</param>
    /// <returns><see langword="true"/> for a draft.</returns>
    public static bool CanEdit(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        return invoice.Status == InvoiceStatus.Draft;
    }

    /// <summary>
    /// Returns whether an invoice may be deleted: only a draft, which has no number and no effect on any balance.
    /// </summary>
    /// <param name="invoice">The invoice.</param>
    /// <returns><see langword="true"/> for a draft.</returns>
    public static bool CanDelete(Invoice invoice)
        => CanEdit(invoice);

    /// <summary>
    /// Returns whether an invoice may be voided: an issued invoice with no payment applied. One that was paid needs a
    /// credit note instead.
    /// </summary>
    /// <param name="invoice">The invoice.</param>
    /// <returns><see langword="true"/> when it may be voided.</returns>
    public static bool CanVoid(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        return CanMove(invoice.Status, InvoiceStatus.Void) && invoice.AmountPaid <= 0m;
    }

    /// <summary>
    /// Returns whether an invoice may be written off as uncollectible: an issued invoice with something still owed.
    /// </summary>
    /// <param name="invoice">The invoice.</param>
    /// <returns><see langword="true"/> when it may be written off.</returns>
    public static bool CanWriteOff(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        return CanMove(invoice.Status, InvoiceStatus.Uncollectible) && invoice.AmountDue > 0m;
    }

    /// <summary>
    /// Returns whether an invoice is overdue on a local date: issued, not paid in full, and due before that date. Overdue
    /// is never stored.
    /// </summary>
    /// <param name="invoice">The invoice.</param>
    /// <param name="today">The local date in the site's time zone.</param>
    /// <returns><see langword="true"/> when it is overdue.</returns>
    public static bool IsOverdue(Invoice invoice, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        return invoice.Status == InvoiceStatus.Open &&
            invoice.Settlement != InvoiceSettlement.Paid &&
            invoice.DueDate is { } due &&
            due < today;
    }

    /// <summary>
    /// Returns the delivery status after an event, which only moves forward: an invoice the customer viewed stays viewed
    /// when it is sent again.
    /// </summary>
    /// <param name="current">The current delivery status.</param>
    /// <param name="next">The delivery status the event reports.</param>
    /// <returns>The later of the two.</returns>
    public static InvoiceDelivery AdvanceDelivery(InvoiceDelivery current, InvoiceDelivery next)
        => next > current ? next : current;
}
