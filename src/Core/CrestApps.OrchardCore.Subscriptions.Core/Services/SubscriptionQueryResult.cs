namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

public class SubscriptionQueryResult
{
    public IEnumerable<SubscriptionSession> Subscriptions { get; set; }

    public int TotalCount { get; set; }
}
