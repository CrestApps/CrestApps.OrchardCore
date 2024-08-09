namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public class StripeSetupIntentMetadata
{
    public string CustomerId { get; set; }

    public string PaymentMethodId { get; set; }
}
