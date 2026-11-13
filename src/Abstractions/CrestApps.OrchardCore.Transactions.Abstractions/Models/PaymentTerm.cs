using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Transactions.Models;

/// <summary>
/// A payment term: when a debt is due after the day its document is issued, such as "Net 30" or "Due on receipt", and an
/// optional discount for paying early, such as 2% within 10 days. It is a small catalog the store keeps once, which
/// invoices, account terms, pay-later purchases and work orders read, so a term is never typed as a bare number of days.
/// <see cref="Services.IPaymentTermCalculator"/> turns it into dates.
/// </summary>
public sealed class PaymentTerm : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the name shown to staff and printed on documents, such as <c>Net 30</c>.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a note about when to use the term.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets how the due date follows from the issue date.
    /// </summary>
    public PaymentTermKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the number of days: after the issue date for <see cref="PaymentTermKind.NetDays"/>, and after the end
    /// of the issue month for <see cref="PaymentTermKind.EndOfMonth"/>. Ignored by the other kinds.
    /// </summary>
    public int Days { get; set; }

    /// <summary>
    /// Gets or sets the day of the month after the issue month the debt is due on, from 1 to 31, for
    /// <see cref="PaymentTermKind.DayOfNextMonth"/>. A day the month does not have is its last day.
    /// </summary>
    public int DayOfMonth { get; set; }

    /// <summary>
    /// Gets or sets the percent taken off when the debt is paid early, such as <c>2</c> for 2%.
    /// <see langword="null"/> or zero offers no discount.
    /// </summary>
    public decimal? DiscountPercent { get; set; }

    /// <summary>
    /// Gets or sets the number of days after the issue date the early-payment discount is offered for, the last day
    /// included. <see langword="null"/> or zero offers no discount.
    /// </summary>
    public int? DiscountDays { get; set; }

    /// <summary>
    /// Gets or sets whether the term may be chosen for new documents. A term that is turned off stays on the documents
    /// that already use it.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC time the term was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the term was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
