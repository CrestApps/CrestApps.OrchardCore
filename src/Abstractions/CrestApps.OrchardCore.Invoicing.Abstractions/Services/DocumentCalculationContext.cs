using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// What the document calculator works on: the lines and charges of a document and the rules that apply to them. The
/// calculator writes the computed amounts back onto the lines and charges it is given.
/// </summary>
public sealed class DocumentCalculationContext
{
    /// <summary>
    /// Gets or sets the ISO-4217 currency of the amounts, which sets the rounding.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets whether the unit prices and charge amounts include tax.
    /// </summary>
    public bool PricesIncludeTax { get; set; }

    /// <summary>
    /// Gets or sets the lines.
    /// </summary>
    public IList<DocumentLine> Lines { get; set; } = [];

    /// <summary>
    /// Gets or sets the discounts, shipping and surcharges on the whole document.
    /// </summary>
    public IList<DocumentCharge> Charges { get; set; } = [];

    /// <summary>
    /// Gets or sets how tax applies to the bill-to, the treatment of every line and charge that sets none of its own.
    /// Anything but <see cref="TaxTreatment.Taxable"/> charges no tax.
    /// </summary>
    public TaxTreatment TaxTreatment { get; set; }

    /// <summary>
    /// Gets or sets why no tax is charged, copied onto the breakdown.
    /// </summary>
    public string ExemptionReason { get; set; }

    /// <summary>
    /// Gets or sets the smallest amount the total is rounded to for cash payment, or <see langword="null"/>.
    /// </summary>
    public decimal? CashRoundingIncrement { get; set; }

    /// <summary>
    /// Gets or sets the party billed, which a tax provider reads for its address and tax profile.
    /// </summary>
    public DocumentParty BillTo { get; set; }

    /// <summary>
    /// Gets or sets the local date the document is issued, which a tax provider reads for the rates in force.
    /// </summary>
    public DateOnly? IssueDate { get; set; }

    /// <summary>
    /// Gets or sets whether a tax provider may set the taxes of the lines. When <see langword="false"/>, the taxes typed
    /// on the lines are used as they are.
    /// </summary>
    public bool AutomaticTax { get; set; }

    /// <summary>
    /// Gets or sets whether lines that already carry taxes keep them, so a tax provider taxes only the lines without any.
    /// A credit note sets it: it credits the tax that was charged (its lines copy the invoice's, or the rates of the
    /// payment's tax snapshot), never what today's rules would give.
    /// </summary>
    public bool KeepTypedTaxes { get; set; }
}
