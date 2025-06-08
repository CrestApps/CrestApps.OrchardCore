namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public abstract class SubscriptionFlowContextBase
{
    public SubscriptionFlow Flow { get; }

    public SubscriptionFlowContextBase(SubscriptionFlow flow)
    {
        ArgumentNullException.ThrowIfNull(flow);

        Flow = flow;
    }
}
