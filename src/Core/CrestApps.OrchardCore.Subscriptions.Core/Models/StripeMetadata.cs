namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public class StripeMetadata
{
    public string CustomerId { get; set; }

    public string PaymentMethodId { get; set; }

    public string SetupIntentId { get; set; }

    public string PaymentIntentId { get; set; }

    public string SubscriptionId { get; set; }
}
