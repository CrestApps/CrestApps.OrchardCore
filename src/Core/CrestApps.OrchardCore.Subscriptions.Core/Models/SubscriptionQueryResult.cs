namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public class SubscriptionQueryResult
{
    public IEnumerable<SubscriptionSession> Subscriptions { get; set; }

    public int TotalCount { get; set; }
}
