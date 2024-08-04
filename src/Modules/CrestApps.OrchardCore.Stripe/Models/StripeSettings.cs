namespace CrestApps.OrchardCore.Stripe.Core;

public sealed class StripeSettings
{
    public bool IsLive { get; set; }

    public string LivePublishableKey { get; set; }

    public string LivePrivateSecret { get; set; }

    public string LiveWebhookSecret { get; set; }

    public string TestingPublishableKey { get; set; }

    public string TestingPrivateSecret { get; set; }

    public string TestingWebhookSecret { get; set; }
}
