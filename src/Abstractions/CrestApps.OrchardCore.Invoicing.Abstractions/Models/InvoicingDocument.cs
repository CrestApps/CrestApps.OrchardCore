using CrestApps.Core.Models;
using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.Invoicing.Models;

/// <summary>
/// What every invoicing document has: the parties, the lines, charges, tax breakdown and totals, the dates and the
/// texts printed on it, and its history. <see cref="Invoice"/> and <see cref="CreditNote"/> add their own life cycle.
/// After a document is issued, its parties, lines, charges, taxes, totals, dates, currency and number never change.
/// </summary>
public abstract class InvoicingDocument : CatalogItem, IAuditedModel
{
    /// <summary>
    /// Gets or sets the YesSql document id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the number, such as <c>INV-1001</c>. Empty until the document is issued.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the party the document is billed to, with its billing details frozen at issue.
    /// </summary>
    public DocumentParty BillTo { get; set; } = new();

    /// <summary>
    /// Gets or sets the business that issues the document, copied at issue.
    /// </summary>
    public DocumentIssuer Issuer { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 code of the currency of every amount on the document.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets how many units of <see cref="BaseCurrency"/> one unit of <see cref="Currency"/> was worth at issue,
    /// when they differ.
    /// </summary>
    public decimal? ExchangeRate { get; set; }

    /// <summary>
    /// Gets or sets the currency the store reports in, which <see cref="ExchangeRate"/> converts to.
    /// </summary>
    public string BaseCurrency { get; set; }

    /// <summary>
    /// Gets or sets whether the unit prices and the charge amounts include tax.
    /// </summary>
    public bool PricesIncludeTax { get; set; }

    /// <summary>
    /// Gets or sets the lines, in the order they are printed.
    /// </summary>
    public IList<DocumentLine> Lines { get; set; } = [];

    /// <summary>
    /// Gets or sets the discounts, shipping and surcharges on the whole document.
    /// </summary>
    public IList<DocumentCharge> Charges { get; set; } = [];

    /// <summary>
    /// Gets or sets the tax breakdown, one row per tax and rate. Computed.
    /// </summary>
    public IList<DocumentTax> Taxes { get; set; } = [];

    /// <summary>
    /// Gets or sets the totals. Computed.
    /// </summary>
    public DocumentTotals Totals { get; set; } = new();

    /// <summary>
    /// Gets or sets the local date the document is, or was, issued, in the site's time zone.
    /// </summary>
    public DateOnly? IssueDate { get; set; }

    /// <summary>
    /// Gets or sets the first day of the period the document bills, when it bills one.
    /// </summary>
    public DateOnly? ServiceStart { get; set; }

    /// <summary>
    /// Gets or sets the last day of the period the document bills, when it bills one.
    /// </summary>
    public DateOnly? ServiceEnd { get; set; }

    /// <summary>
    /// Gets or sets the customer's own reference, such as a purchase order number.
    /// </summary>
    public string PurchaseOrderNumber { get; set; }

    /// <summary>
    /// Gets or sets the store's reference.
    /// </summary>
    public string Reference { get; set; }

    /// <summary>
    /// Gets or sets the kind of record the document was made from, such as an estimate, a recurring plan, a work order or
    /// an order.
    /// </summary>
    public string SourceType { get; set; }

    /// <summary>
    /// Gets or sets the id of the record the document was made from.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets notes printed on the document for the customer.
    /// </summary>
    public string Notes { get; set; }

    /// <summary>
    /// Gets or sets the terms and conditions printed on the document.
    /// </summary>
    public string Terms { get; set; }

    /// <summary>
    /// Gets or sets the footer printed on the document.
    /// </summary>
    public string Footer { get; set; }

    /// <summary>
    /// Gets or sets a note for staff, never shown to the customer. It may change after issue.
    /// </summary>
    public string InternalNote { get; set; }

    /// <summary>
    /// Gets or sets extra labeled values printed on the document, such as a project code.
    /// </summary>
    public IDictionary<string, string> CustomFields { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or sets the id of the business document template the document is printed with, or <see langword="null"/> for
    /// the default.
    /// </summary>
    public string TemplateId { get; set; }

    /// <summary>
    /// Gets or sets the culture the document is printed and sent in, such as <c>fr-CA</c>.
    /// </summary>
    public string Culture { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the document was issued.
    /// </summary>
    public DateTime? IssuedUtc { get; set; }

    /// <summary>
    /// Gets or sets a hash of the issued content, which shows the document was not changed after issue.
    /// </summary>
    public string ContentHash { get; set; }

    /// <summary>
    /// Gets or sets the path of the PDF stored when the document was issued, in the tenant's private file store.
    /// </summary>
    public string ArchiveFile { get; set; }

    /// <summary>
    /// Gets or sets the append-only history.
    /// </summary>
    public IList<DocumentEvent> History { get; set; } = [];

    /// <inheritdoc/>
    public DateTime CreatedUtc { get; set; }

    /// <inheritdoc/>
    public DateTime? ModifiedUtc { get; set; }

    /// <inheritdoc/>
    public string OwnerId { get; set; }

    /// <inheritdoc/>
    public string Author { get; set; }
}
