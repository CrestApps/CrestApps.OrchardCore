namespace CrestApps.OrchardCore.Stripe.Core.Models;

public class SubscriptionLineItem
{
    public int Quantity { get; set; }

    public string PriceId { get; set; }
}
