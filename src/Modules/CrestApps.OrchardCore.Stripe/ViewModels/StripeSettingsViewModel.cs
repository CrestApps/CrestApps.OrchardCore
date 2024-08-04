namespace CrestApps.OrchardCore.Stripe.ViewModels;

public class StripeSettingsViewModel
{
    public bool IsLive { get; set; }

    public string LivePublishableKey { get; set; }

    public bool HasLivePrivateSecret { get; set; }

    public string LivePrivateSecret { get; set; }

    public bool HasLiveWebhookSecret { get; set; }

    public string LiveWebhookSecret { get; set; }

    public string TestingPublishableKey { get; set; }

    public bool HasTestingPrivateSecret { get; set; }

    public string TestingPrivateSecret { get; set; }

    public bool HasTestingWebhookSecret { get; set; }

    public string TestingWebhookSecret { get; set; }
}
