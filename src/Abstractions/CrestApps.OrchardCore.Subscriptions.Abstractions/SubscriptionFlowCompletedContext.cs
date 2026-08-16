namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Provides context after a subscription flow has completed.
/// </summary>
public sealed class SubscriptionFlowCompletedContext : SubscriptionFlowContextBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionFlowCompletedContext"/> class.
    /// </summary>
    /// <param name="flow">The subscription flow that completed.</param>
    public SubscriptionFlowCompletedContext(SubscriptionFlow flow)
        : base(flow)
    {
    }
}
