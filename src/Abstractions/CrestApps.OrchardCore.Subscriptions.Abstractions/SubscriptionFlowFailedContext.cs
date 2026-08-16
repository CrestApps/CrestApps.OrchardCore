namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Provides context for handlers that run after a subscription flow fails.
/// </summary>
public sealed class SubscriptionFlowFailedContext : SubscriptionFlowContextBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionFlowFailedContext"/> class.
    /// </summary>
    /// <param name="flow">The subscription flow that failed.</param>
    public SubscriptionFlowFailedContext(SubscriptionFlow flow)
        : base(flow)
    {
    }
}
