namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Provides context for handlers that run after a subscription flow is loaded.
/// </summary>
public sealed class SubscriptionFlowLoadedContext : SubscriptionFlowContextBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionFlowLoadedContext"/> class.
    /// </summary>
    /// <param name="flow">The loaded subscription flow.</param>
    public SubscriptionFlowLoadedContext(SubscriptionFlow flow)
        : base(flow)
    {
    }
}
