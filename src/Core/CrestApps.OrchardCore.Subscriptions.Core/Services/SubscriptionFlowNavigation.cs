using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

public class SubscriptionFlowNavigation
{
    [BindNever]
    public string PreviousStep { get; set; }

    [BindNever]
    public string CurrentStep { get; set; }

    [BindNever]
    public string NextStep { get; set; }

    [BindNever]
    public bool IsPaymentStep { get; set; }

    [BindNever]
    public string SessionId { get; set; }
}
