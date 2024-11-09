
namespace CrestApps.OrchardCore.Stripe.Core.Models;

public class CreateSubscriptionLineItem
{
    public int Quantity { get; set; }

    public string PriceId { get; set; }

    public Dictionary<string, string> Metadata { get; set; }
}
