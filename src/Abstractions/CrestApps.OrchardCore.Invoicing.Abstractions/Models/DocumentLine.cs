using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Invoicing.Models;

/// <summary>
/// A line of an invoicing document (invoice, credit note, estimate, recurring plan). The inputs are typed or copied from
/// a source; <see cref="Net"/>, <see cref="DocumentDiscount"/>, <see cref="TaxAmount"/> and <see cref="Total"/> are
/// computed by the document calculator and are never typed.
/// </summary>
public sealed class DocumentLine
{
    /// <summary>
    /// Gets or sets the line's id, unique within the document, so a credit note can name the line it credits.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets what the line bills.
    /// </summary>
    public DocumentLineKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the id of the product the line sells, when it sells one.
    /// </summary>
    public string ProductId { get; set; }

    /// <summary>
    /// Gets or sets the product's stock keeping unit, copied when the line was added.
    /// </summary>
    public string Sku { get; set; }

    /// <summary>
    /// Gets or sets the name printed on the document.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a longer description printed under the name.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the quantity, up to six decimals. It may be negative to take an amount off.
    /// </summary>
    public decimal Quantity { get; set; } = 1m;

    /// <summary>
    /// Gets or sets the unit of the quantity, such as <c>h</c> or <c>pcs</c>.
    /// </summary>
    public string Unit { get; set; }

    /// <summary>
    /// Gets or sets the price of one unit, up to six decimals: net of tax, or including it when the document's prices
    /// include tax.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Gets or sets the discount on the line in percent, such as <c>10</c>. Ignored when
    /// <see cref="DiscountAmount"/> is set.
    /// </summary>
    public decimal? DiscountPercent { get; set; }

    /// <summary>
    /// Gets or sets the discount on the line as an amount, in the document's currency.
    /// </summary>
    public decimal? DiscountAmount { get; set; }

    /// <summary>
    /// Gets or sets the tax category of the line, such as a Taxation category code, which a tax provider reads to find
    /// the line's taxes.
    /// </summary>
    public string TaxCategoryCode { get; set; }

    /// <summary>
    /// Gets or sets the taxes of the line: typed on the document, or set by a tax provider.
    /// </summary>
    public IList<DocumentLineTax> Taxes { get; set; } = [];

    /// <summary>
    /// Gets or sets how tax applies to this line, such as a zero-rated or exempt line on an otherwise taxable invoice.
    /// <see langword="null"/> takes the document's treatment, which comes from the bill-to.
    /// </summary>
    public TaxTreatment? TaxTreatment { get; set; }

    /// <summary>
    /// Gets or sets why no tax is charged on the line, for a treatment that is not taxable, printed on the breakdown.
    /// <see langword="null"/> takes the document's reason.
    /// </summary>
    public string TaxTreatmentReason { get; set; }

    /// <summary>
    /// Gets or sets the first day of the period the line bills, when it bills one.
    /// </summary>
    public DateOnly? ServiceStart { get; set; }

    /// <summary>
    /// Gets or sets the last day of the period the line bills, when it bills one.
    /// </summary>
    public DateOnly? ServiceEnd { get; set; }

    /// <summary>
    /// Gets or sets the kind of record the line was billed from, such as a time entry or a work order line.
    /// </summary>
    public string SourceType { get; set; }

    /// <summary>
    /// Gets or sets the id of the record the line was billed from.
    /// </summary>
    public string SourceId { get; set; }

    /// <summary>
    /// Gets or sets, on a credit note, the id of the invoice line it credits.
    /// </summary>
    public string CreditedLineId { get; set; }

    /// <summary>
    /// Gets or sets the line's net amount: the quantity times the unit price less the line discount, rounded to the
    /// currency, without tax. Computed.
    /// </summary>
    public decimal Net { get; set; }

    /// <summary>
    /// Gets or sets the line's share of the document's discounts, net of tax. Computed.
    /// </summary>
    public decimal DocumentDiscount { get; set; }

    /// <summary>
    /// Gets or sets the tax of the line, its share of the breakdown. Computed.
    /// </summary>
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// Gets or sets the line's total: its net, less its share of the document's discounts, plus its tax. Computed.
    /// </summary>
    public decimal Total { get; set; }

    /// <summary>
    /// Gets whether the line carries an amount.
    /// </summary>
    public bool HasAmount
        => Kind is not (DocumentLineKind.Text or DocumentLineKind.Section);
}
