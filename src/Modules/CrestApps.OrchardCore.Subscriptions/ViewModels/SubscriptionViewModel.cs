using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public class SubscriptionViewModel : ShapeViewModel
{
    public SubscriptionSession Subscription { get; set; }

    public SubscriptionViewModel()
    {
    }

    public SubscriptionViewModel(SubscriptionSession subscription)
    {
        Subscription = subscription;
    }
}
