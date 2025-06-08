namespace CrestApps.OrchardCore.Stripe.Models;

public sealed class StripeSettings
{
    public bool IsLive { get; set; }

    public string LivePublishableKey { get; set; }

    public string LivePrivateSecret { get; set; }

    public string LiveWebhookSecret { get; set; }

    public string TestPublishableKey { get; set; }

    public string TestPrivateSecret { get; set; }

    public string TestWebhookSecret { get; set; }
}
