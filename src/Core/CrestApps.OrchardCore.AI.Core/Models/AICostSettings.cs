namespace CrestApps.OrchardCore.AI.Core.Models;

/// <summary>
/// What the phone lines cost, for the estimated cost of each AI call. What the AI costs is set on each AI deployment
/// (<c>AIDeploymentPricing</c>).
/// </summary>
public sealed class AICostSettings
{
    /// <summary>
    /// Gets or sets the currency the carrier's prices are in, such as <c>USD</c>.
    /// </summary>
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Gets or sets what a minute of a phone call costs with the carrier.
    /// </summary>
    public decimal TelephonyPerMinute { get; set; }

    /// <summary>
    /// Gets or sets what one text message costs with the carrier.
    /// </summary>
    public decimal SmsPerMessage { get; set; }
}

