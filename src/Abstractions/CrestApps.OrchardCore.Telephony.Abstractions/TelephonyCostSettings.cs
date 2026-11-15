namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Represents what the phone carrier charges, used to estimate the cost of a call.
/// </summary>
public sealed class TelephonyCostSettings
{
    /// <summary>
    /// Gets or sets the currency the carrier's prices are in, such as <c>USD</c>.
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Gets or sets what a minute of a phone call costs with the carrier.
    /// </summary>
    public decimal PerMinute { get; set; }
}
