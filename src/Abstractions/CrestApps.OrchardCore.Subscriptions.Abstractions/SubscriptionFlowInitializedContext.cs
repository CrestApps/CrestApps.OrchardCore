namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Provides context for handlers that run after a subscription flow is initialized.
/// </summary>
public sealed class SubscriptionFlowInitializedContext : SubscriptionFlowContextBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionFlowInitializedContext"/> class.
    /// </summary>
    /// <param name="flow">The initialized subscription flow.</param>
    public SubscriptionFlowInitializedContext(SubscriptionFlow flow)
        : base(flow)
    {
    }
}
