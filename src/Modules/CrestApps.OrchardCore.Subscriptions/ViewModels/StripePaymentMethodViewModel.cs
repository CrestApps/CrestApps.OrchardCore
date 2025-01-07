using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public class StripePaymentMethodViewModel
{
    [BindNever]
    public string SessionId { get; set; }

    [BindNever]
    public bool IsLive { get; set; }

    [BindNever]
    public string PublishableKey { get; set; }
}
