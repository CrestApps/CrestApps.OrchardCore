namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// The output produced by an <see cref="Services.ITaxCalculationMethod"/>.
/// </summary>
public sealed class TaxComputationResult
{
    /// <summary>
    /// Gets or sets the taxable base the tax was effectively calculated on. For tax-inclusive pricing
    /// this may differ from the requested base because the tax is extracted from it.
    /// </summary>
    public decimal TaxableAmount { get; set; }

    /// <summary>
    /// Gets or sets the calculated tax amount.
    /// </summary>
    public decimal TaxAmount { get; set; }

    /// <summary>
    /// Gets or sets the effective rate that was applied, when the method is rate based.
    /// </summary>
    public decimal EffectiveRate { get; set; }
}
