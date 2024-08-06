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

    public string TestPublishableKey { get; set; }

    [BindNever]
    public bool HasTestPrivateSecret { get; set; }

    public string TestPrivateSecret { get; set; }

    [BindNever]
    public bool HasTestWebhookSecret { get; set; }

    public string TestWebhookSecret { get; set; }
}
