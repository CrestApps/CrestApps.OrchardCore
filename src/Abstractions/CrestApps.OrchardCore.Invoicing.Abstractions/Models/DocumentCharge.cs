using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Invoicing.Models;

/// <summary>
/// A discount, shipping or surcharge on a whole document. Its <see cref="Net"/> and <see cref="TaxAmount"/> are
/// computed by the document calculator.
/// </summary>
public sealed class DocumentCharge
{
    /// <summary>
    /// Gets or sets the charge's id, unique within the document.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets what the charge is.
    /// </summary>
    public DocumentChargeKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the name printed on the document, such as <c>Loyalty discount</c> or <c>Freight</c>.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets, for a discount, the percent of the discountable lines it takes off. Ignored when
    /// <see cref="Amount"/> is set, and by shipping and surcharges.
    /// </summary>
    public decimal? Percent { get; set; }

    /// <summary>
    /// Gets or sets the amount of the charge, positive, net of tax or including it when the document's prices include
    /// tax. A discount takes it off.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// Gets or sets the tax category of a shipping or surcharge, which a tax provider reads.
    /// </summary>
    public string TaxCategoryCode { get; set; }

    /// <summary>
    /// Gets or sets the taxes of a shipping or surcharge. A discount takes the taxes of the lines it is spread over.
    /// </summary>
    public IList<DocumentLineTax> Taxes { get; set; } = [];

    /// <summary>
    /// Gets or sets how tax applies to a shipping or surcharge. <see langword="null"/> takes the document's treatment.
    /// </summary>
    public TaxTreatment? TaxTreatment { get; set; }

    /// <summary>
    /// Gets or sets why no tax is charged on a shipping or surcharge, for a treatment that is not taxable.
    /// </summary>
    public string TaxTreatmentReason { get; set; }

    /// <summary>
    /// Gets or sets the net amount of the charge, positive, rounded to the currency. Computed.
    /// </summary>
    public decimal Net { get; set; }

    /// <summary>
    /// Gets or sets the tax of a shipping or surcharge. Computed.
    /// </summary>
    public decimal TaxAmount { get; set; }
}
