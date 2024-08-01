using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public class SubscriptionViewModel
{
    public string SessionId { get; set; }

    public IShape Content { get; set; }
}
