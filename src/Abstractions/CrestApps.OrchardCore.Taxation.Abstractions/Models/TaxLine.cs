namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// Represents a single applied tax and explains how it was determined and calculated.
/// </summary>
public sealed class TaxLine
{
    /// <summary>
    /// Gets or sets the identifier of the taxable item this line applies to.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the code of the applied tax.
    /// </summary>
    public string TaxCode { get; set; }

    /// <summary>
    /// Gets or sets the human readable name of the applied tax.
    /// </summary>
    public string TaxName { get; set; }

    /// <summary>
    /// Gets or sets the type of the applied tax.
    /// </summary>
    public string TaxType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the jurisdiction that levied the tax.
    /// </summary>
    public string JurisdictionId { get; set; }

    /// <summary>
    /// Gets or sets the name of the jurisdiction that levied the tax.
    /// </summary>
    public string JurisdictionName { get; set; }

    /// <summary>
    /// Gets or sets the effective rate that was applied, when the method is rate based.
    /// </summary>
    public decimal Rate { get; set; }

    /// <summary>
    /// Gets or sets the taxable base the tax was calculated on.
    /// </summary>
    public decimal TaxableAmount { get; set; }

    /// <summary>
    /// Gets or sets the calculated tax amount.
    /// </summary>
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// Gets or sets the name of the calculation method that produced the amount.
    /// </summary>
    public string CalculationMethod { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tax was included in the item price.
    /// </summary>
    public bool IncludedInPrice { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the tax is compound (calculated on top of other taxes).
    /// </summary>
    public bool IsCompound { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the rule that produced the line.
    /// </summary>
    public string RuleId { get; set; }

    /// <summary>
    /// Gets or sets the version of the rule that produced the line.
    /// </summary>
    public int RuleVersion { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the tax table used, when applicable.
    /// </summary>
    public string TableId { get; set; }

    /// <summary>
    /// Gets or sets the version of the tax table used, when applicable.
    /// </summary>
    public int TableVersion { get; set; }
}
