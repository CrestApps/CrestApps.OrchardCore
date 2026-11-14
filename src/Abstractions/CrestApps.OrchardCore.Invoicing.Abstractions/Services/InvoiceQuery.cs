using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// Filters a page of invoices. Every filter that is set must match.
/// </summary>
public sealed class InvoiceQuery
{
    /// <summary>
    /// Gets or sets text found in the invoice number, the bill-to name, the reference or the purchase order number.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public InvoiceStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the settlement.
    /// </summary>
    public InvoiceSettlement? Settlement { get; set; }

    /// <summary>
    /// Gets or sets the parties billed: an invoice is listed when its bill-to is one of them.
    /// </summary>
    public IList<PartyRef> BillTo { get; set; } = [];

    /// <summary>
    /// Gets or sets a date: only open invoices due before it are listed (overdue on that date).
    /// </summary>
    public DateOnly? OverdueOn { get; set; }

    /// <summary>
    /// Gets or sets the kind of record the invoice was made from, such as <c>WorkOrder</c>.
    /// </summary>
    public string SourceType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the record the invoice was made from.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets whether only issued invoices are returned, never drafts.
    /// </summary>
    public bool IssuedOnly { get; set; }

    /// <summary>
    /// Gets or sets whether only invoices billed to <see cref="BillTo"/> are returned, so an empty list returns none
    /// rather than every invoice.
    /// </summary>
    public bool OnlyBillTo { get; set; }
}
