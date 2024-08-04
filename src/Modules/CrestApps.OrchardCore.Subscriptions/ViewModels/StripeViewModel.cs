using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public class StripeViewModel
{
    public string CustomerId { get; set; }

    public string PaymentMethodId { get; set; }

    [BindNever]
    public bool IsLive { get; set; }

    [BindNever]
    public string PublishableKey { get; set; }
}
