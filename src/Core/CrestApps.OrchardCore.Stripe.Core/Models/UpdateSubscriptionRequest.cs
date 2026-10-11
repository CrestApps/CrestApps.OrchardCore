namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents a request to change what a Stripe subscription bills.
/// </summary>
public class UpdateSubscriptionRequest : StripeWriteRequest
{
    /// <summary>
    /// Gets or sets the Stripe subscription identifier.
    /// </summary>
    public string SubscriptionId { get; set; }

    /// <summary>
    /// Gets or sets the new quantity for the subscription's single item.
    /// </summary>
    public int Quantity { get; set; } = 1;

    /// <summary>
    /// Gets or sets the replacement price, defined inline so any amount can be billed.
    /// </summary>
    public SubscriptionInlinePrice Price { get; set; }

    /// <summary>
    /// Gets or sets Stripe's proration behavior: <c>create_prorations</c>, <c>none</c>, or
    /// <c>always_invoice</c>.
    /// </summary>
    public string ProrationBehavior { get; set; } = "create_prorations";
}
