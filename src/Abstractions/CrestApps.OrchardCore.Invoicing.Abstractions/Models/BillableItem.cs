using CrestApps.Core.Models;
using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Invoicing.Models;

/// <summary>
/// A charge to bill a customer on a later invoice, entered by staff or a system: an expense passed on with a markup,
/// work done, a fee. It stays unbilled until an invoice line bills it, and is let go again if that invoice is voided or
/// deleted, or the line is taken off or credited.
/// </summary>
public sealed class BillableItem : CatalogItem
{
    /// <summary>
    /// Gets or sets who it is billed to.
    /// </summary>
    public PartyRef Party { get; set; }

    /// <summary>
    /// Gets or sets the customer's name, kept for lists.
    /// </summary>
    public string PartyName { get; set; }

    /// <summary>
    /// Gets or sets what it is: <see cref="DocumentLineKind.Expense"/>, <see cref="DocumentLineKind.Time"/>,
    /// <see cref="DocumentLineKind.Service"/>, <see cref="DocumentLineKind.Item"/> or <see cref="DocumentLineKind.Fee"/>.
    /// </summary>
    public DocumentLineKind Kind { get; set; } = DocumentLineKind.Expense;

    /// <summary>
    /// Gets or sets the day it was incurred, in the site's time zone.
    /// </summary>
    public DateOnly Date { get; set; }

    public string Description { get; set; }

    public decimal Quantity { get; set; } = 1m;

    public string Unit { get; set; }

    /// <summary>
    /// Gets or sets what it cost the business, each.
    /// </summary>
    public decimal UnitCost { get; set; }

    /// <summary>
    /// Gets or sets the percent added to the cost to bill it, such as 10 for an expense passed on at cost plus 10%.
    /// </summary>
    public decimal MarkupPercent { get; set; }

    public string Currency { get; set; }

    public string TaxCategoryCode { get; set; }

    /// <summary>
    /// Gets or sets the invoice that bills it, while one does.
    /// </summary>
    public string InvoiceId { get; set; }

    /// <summary>
    /// Gets or sets the line of <see cref="InvoiceId"/> that bills it.
    /// </summary>
    public string InvoiceLineId { get; set; }

    /// <summary>
    /// Gets or sets what made it in another system, with <see cref="SourceId"/>.
    /// </summary>
    public string SourceType { get; set; }

    public string SourceId { get; set; }

    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets the price billed for each: the cost plus the markup, rounded to the cent.
    /// </summary>
    public decimal UnitPrice
        => Math.Round(UnitCost * (1m + (MarkupPercent / 100m)), 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Gets whether an invoice bills it.
    /// </summary>
    public bool IsBilled
        => !string.IsNullOrEmpty(InvoiceId);
}

/// <summary>
/// Something a billable source offers to put on an invoice.
/// </summary>
public sealed class BillableCandidate
{
    /// <summary>
    /// Gets or sets the source's type, such as <c>charges</c> or <c>time</c>.
    /// </summary>
    public string Source { get; set; }

    public string Id { get; set; }

    public DateOnly Date { get; set; }

    public DocumentLineKind Kind { get; set; }

    public string Description { get; set; }

    public decimal Quantity { get; set; }

    public string Unit { get; set; }

    public decimal UnitPrice { get; set; }

    public string Currency { get; set; }

    public string TaxCategoryCode { get; set; }
}

/// <summary>
/// What billable work to list.
/// </summary>
public sealed class BillableQuery
{
    /// <summary>
    /// Gets or sets the customer the charges are for.
    /// </summary>
    public PartyRef Party { get; set; }

    /// <summary>
    /// Gets or sets the currency of the invoice; a charge in another currency is not offered.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets whose time to list (the time source).
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the first day, in the site's time zone.
    /// </summary>
    public DateOnly? From { get; set; }

    /// <summary>
    /// Gets or sets the last day, in the site's time zone.
    /// </summary>
    public DateOnly? Through { get; set; }

    /// <summary>
    /// Gets or sets the hourly rate the time is billed at (the time source).
    /// </summary>
    public decimal? HourlyRate { get; set; }
}
