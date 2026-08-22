namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Provides context for handlers that run while a subscription flow is being loaded.
/// </summary>
public sealed class SubscriptionFlowLoadingContext : SubscriptionFlowContextBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionFlowLoadingContext"/> class.
    /// </summary>
    /// <param name="flow">The subscription flow being loaded.</param>
    public SubscriptionFlowLoadingContext(SubscriptionFlow flow)
        : base(flow)
    {
    }
}
