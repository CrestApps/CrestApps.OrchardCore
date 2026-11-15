namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// A tax that applies to an item of a document's tax breakdown.
/// </summary>
public sealed class TaxBreakdownRate
{
    /// <summary>
    /// Gets or sets how the tax applies: taxable, or a zero rate, an exemption or a reverse charge the rules found.
    /// </summary>
    public TaxTreatment Treatment { get; set; }

    /// <summary>
    /// Gets or sets why the tax is not charged, for a treatment that is not taxable.
    /// </summary>
    public string TreatmentReason { get; set; }

    /// <summary>
    /// Gets or sets the tax's code, such as a VAT category.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the tax's name, such as VAT or a state's sales tax.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the rate in percent, such as <c>20</c> for 20%.
    /// </summary>
    public decimal Rate { get; set; }

    /// <summary>
    /// Gets or sets whether the tax is charged on the amount plus the item's other taxes.
    /// </summary>
    public bool IsCompound { get; set; }

    /// <summary>
    /// Gets or sets the kind of tax, such as VAT or a state's sales tax, which rounding per tax groups by.
    /// </summary>
    public string TaxType { get; set; }

    /// <summary>
    /// Gets or sets the jurisdiction that levies the tax, which rounding per jurisdiction groups by.
    /// </summary>
    public string JurisdictionId { get; set; }
}
