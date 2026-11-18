namespace CrestApps.OrchardCore.Taxation.Models;

/// <summary>
/// How the site works tax out, under Settings > Commerce > Tax calculation. The checkout and invoices both read it, so a
/// cart and an invoice with the same lines come to the same tax.
/// </summary>
public sealed class TaxCalculationSettings
{
    /// <summary>
    /// Gets or sets the level tax is rounded at. A new site rounds each tax and rate once on the summed amount
    /// (<see cref="TaxRoundingLevel.Rate"/>, as EN 16931 asks); a site that had Taxation before keeps rounding each line.
    /// </summary>
    public TaxRoundingLevel RoundingLevel { get; set; } = TaxRoundingLevel.Rate;
}
