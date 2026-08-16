
namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents one Stripe price and quantity pair to include in a subscription.
/// </summary>
public class CreateSubscriptionLineItem
{
    /// <summary>
    /// Gets or sets the number of price units to include in the subscription item.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the Stripe price identifier for the subscription item.
    /// </summary>
    public string PriceId { get; set; }

    /// <summary>
    /// Gets or sets the metadata to store with the subscription item in Stripe.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; }
}
