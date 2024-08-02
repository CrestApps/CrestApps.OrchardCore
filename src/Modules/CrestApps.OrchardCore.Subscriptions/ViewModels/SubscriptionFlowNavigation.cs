using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public class SubscriptionFlowNavigation
{
    public string Direction { get; set; }

    [BindNever]
    public string PreviousStep { get; set; }

    [BindNever]
    public string CurrentStep { get; set; }

    [BindNever]
    public string NextStep { get; set; }

    [BindNever]
    public bool IsPaymentStep { get; set; }
}
