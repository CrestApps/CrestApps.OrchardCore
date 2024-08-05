namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public sealed class SubscriptionFlowInitializedContext : SubscriptionFlowContextBase
{
    public SubscriptionFlowInitializedContext(SubscriptionFlow flow)
        : base(flow)
    {
    }
}
