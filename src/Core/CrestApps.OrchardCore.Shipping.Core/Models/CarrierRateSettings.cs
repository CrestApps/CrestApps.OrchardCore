namespace CrestApps.OrchardCore.Shipping.Models;

/// <summary>
/// The settings of a shipping method priced by the carriers: which of their services to offer and what to add.
/// </summary>
public sealed class CarrierRateSettings
{
    /// <summary>
    /// Gets or sets the carriers to offer, such as USPS or UPS; empty offers every carrier the account quotes.
    /// </summary>
    public List<string> Carriers { get; set; } = [];

    /// <summary>
    /// Gets or sets how many of the cheapest services to offer, or 0 for all of them.
    /// </summary>
    public int MaxRates { get; set; } = 3;

    /// <summary>
    /// Gets or sets the percentage added to each quoted rate.
    /// </summary>
    public decimal MarkupPercent { get; set; }

    /// <summary>
    /// Gets or sets the amount added to each quoted rate.
    /// </summary>
    public decimal MarkupAmount { get; set; }

    /// <summary>
    /// Gets or sets what to charge when the carriers can't be reached, or <see langword="null"/> to offer nothing then.
    /// </summary>
    public decimal? FallbackAmount { get; set; }
}
