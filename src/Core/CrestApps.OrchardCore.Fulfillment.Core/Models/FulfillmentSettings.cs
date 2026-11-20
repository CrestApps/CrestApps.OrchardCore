namespace CrestApps.OrchardCore.Fulfillment.Core.Models;

/// <summary>
/// How the store hands paid orders to its fulfillment partners.
/// </summary>
public sealed class FulfillmentSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether paid orders are sent to the partners by themselves. When off, a person
    /// sends each order from its page.
    /// </summary>
    public bool SendAutomatically { get; set; }
}
