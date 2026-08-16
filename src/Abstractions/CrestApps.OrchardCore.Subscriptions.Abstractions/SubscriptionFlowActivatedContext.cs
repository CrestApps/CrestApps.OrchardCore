namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Provides context after a subscription flow has been activated.
/// </summary>
public sealed class SubscriptionFlowActivatedContext : SubscriptionFlowContextBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionFlowActivatedContext"/> class.
    /// </summary>
    /// <param name="flow">The subscription flow that was activated.</param>
    public SubscriptionFlowActivatedContext(SubscriptionFlow flow)
        : base(flow)
    {
    }
}
