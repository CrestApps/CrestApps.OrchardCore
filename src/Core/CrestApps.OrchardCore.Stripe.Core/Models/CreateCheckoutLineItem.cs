namespace CrestApps.OrchardCore.Stripe.Core.Models;

public sealed class CreateCheckoutLineItem
{
    /// <summary>
    /// The identifier of an existing Stripe Price. Required.
    /// </summary>
    public string PriceId { get; set; }

    public long Quantity { get; set; } = 1;
}
