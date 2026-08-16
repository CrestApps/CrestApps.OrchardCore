namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents a line item to include in a Stripe Checkout Session.
/// </summary>
public sealed class CreateCheckoutLineItem
{
    /// <summary>
    /// Gets or sets the identifier of an existing Stripe Price. This value is required.
    /// </summary>
    public string PriceId { get; set; }

    /// <summary>
    /// Gets or sets the quantity to purchase for the line item.
    /// </summary>
    public long Quantity { get; set; } = 1;
}
