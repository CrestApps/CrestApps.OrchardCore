namespace CrestApps.OrchardCore.Stripe.Core;

public sealed class StripeOptions
{
    public bool IsLive { get; set; }

    public string PublishableKey { get; set; }

    public string ApiKey { get; set; }

    public string WebhookSecret { get; set; }
}
