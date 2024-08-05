using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Stripe.ViewModels;

public class StripeSettingsViewModel
{
    public bool IsLive { get; set; }

    public string LivePublishableKey { get; set; }

    [BindNever]
    public bool HasLivePrivateSecret { get; set; }

    public string LivePrivateSecret { get; set; }

    [BindNever]
    public bool HasLiveWebhookSecret { get; set; }

    public string LiveWebhookSecret { get; set; }

    public string TestingPublishableKey { get; set; }

    [BindNever]
    public bool HasTestingPrivateSecret { get; set; }

    public string TestingPrivateSecret { get; set; }

    [BindNever]
    public bool HasTestingWebhookSecret { get; set; }

    public string TestingWebhookSecret { get; set; }
}
