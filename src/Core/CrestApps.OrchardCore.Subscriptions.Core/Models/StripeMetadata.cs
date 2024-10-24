
namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public class StripeMetadata
{
    public string CustomerId { get; set; }

    public string PaymentMethodId { get; set; }

    public string SetupIntentId { get; set; }

    public string PaymentIntentId { get; set; }

    public Dictionary<string, StripeSubscriptionMetadata> Subscriptions { get; set; } = [];
}

public class StripeSubscriptionMetadata
{
    public string SubscriptionId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }
}
