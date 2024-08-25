using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public class SubscriptionViewModel
{
    public string ContentItemId { get; set; }

    public string SessionId { get; set; }

    public string Step { get; set; }

    public IShape Content { get; set; }
}
