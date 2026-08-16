namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Provides context while a subscription flow is completing.
/// </summary>
public sealed class SubscriptionFlowCompletingContext : SubscriptionFlowContextBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionFlowCompletingContext"/> class.
    /// </summary>
    /// <param name="flow">The subscription flow that is completing.</param>
    public SubscriptionFlowCompletingContext(SubscriptionFlow flow)
        : base(flow)
    {
    }
}
