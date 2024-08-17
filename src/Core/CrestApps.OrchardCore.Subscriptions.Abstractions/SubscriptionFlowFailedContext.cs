namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public sealed class SubscriptionFlowFailedContext : SubscriptionFlowContextBase
{
    public SubscriptionFlowFailedContext(SubscriptionFlow flow)
        : base(flow)
    {
    }
}
