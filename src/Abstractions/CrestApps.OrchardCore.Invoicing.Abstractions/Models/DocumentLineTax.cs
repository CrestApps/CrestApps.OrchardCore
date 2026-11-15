using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Invoicing.Models;

/// <summary>
/// A tax that applies to a line or a charge: typed on the document, or set by a tax provider. The calculator computes
/// the amount per tax and rate on the summed net of every line that carries it. A tax whose <see cref="Treatment"/> is
/// not <see cref="TaxTreatment.Taxable"/>, such as a zero rate or an exemption a tax provider found, charges nothing and
/// puts the line in a breakdown row of that treatment, with <see cref="TreatmentReason"/>.
/// </summary>
public sealed class DocumentLineTax
{
    /// <summary>
    /// Gets or sets how the tax applies to the line. Defaults to <see cref="TaxTreatment.Taxable"/>.
    /// </summary>
    public TaxTreatment Treatment { get; set; }

    /// <summary>
    /// Gets or sets why the tax is not charged, for a treatment that is not taxable, printed on the breakdown.
    /// </summary>
    public string TreatmentReason { get; set; }

    /// <summary>
    /// Gets or sets the tax's code, such as <c>VAT</c> or a jurisdiction code. Taxes with the same code, name, rate and
    /// compound flag form one row of the breakdown.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the name printed on the document, such as <c>VAT</c> or <c>State sales tax</c>.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the rate in percent, such as <c>20</c> for 20%.
    /// </summary>
    public decimal Rate { get; set; }

    /// <summary>
    /// Gets or sets whether the tax is charged on the net plus the other taxes of the line, rather than on the net
    /// alone.
    /// </summary>
    public bool IsCompound { get; set; }

    /// <summary>
    /// Gets or sets the kind of tax the rules gave, such as VAT or a state's sales tax, for rounding per tax.
    /// </summary>
    public string TaxType { get; set; }

    /// <summary>
    /// Gets or sets the jurisdiction that levies the tax, for rounding per jurisdiction.
    /// </summary>
    public string JurisdictionId { get; set; }
}
