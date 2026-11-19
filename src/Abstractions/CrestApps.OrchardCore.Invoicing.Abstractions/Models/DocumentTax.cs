using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Invoicing.Models;

/// <summary>
/// One row of a document's tax breakdown: a tax and rate, the summed net it was charged on and the tax. A document that
/// is exempt, reverse charged, zero rated or out of scope has a row with no tax and the reason, which the document
/// prints.
/// </summary>
public sealed class DocumentTax
{
    /// <summary>
    /// Gets or sets the tax's code.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the tax's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets how the tax applies.
    /// </summary>
    public TaxTreatment Treatment { get; set; }

    /// <summary>
    /// Gets or sets the rate in percent.
    /// </summary>
    public decimal Rate { get; set; }

    /// <summary>
    /// Gets or sets the amount the tax was charged on.
    /// </summary>
    public decimal TaxableAmount { get; set; }

    /// <summary>
    /// Gets or sets the tax.
    /// </summary>
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// Gets or sets whether the tax was charged on the net plus the other taxes.
    /// </summary>
    public bool IsCompound { get; set; }

    /// <summary>
    /// Gets or sets why no tax was charged, for a row that is not taxable.
    /// </summary>
    public string ExemptionReason { get; set; }
}
