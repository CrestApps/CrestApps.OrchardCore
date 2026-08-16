namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Provides a base context for subscription flow events that operate on an existing flow.
/// </summary>
public abstract class SubscriptionFlowContextBase
{
    /// <summary>
    /// Gets the subscription flow associated with the event.
    /// </summary>
    public SubscriptionFlow Flow { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionFlowContextBase"/> class.
    /// </summary>
    /// <param name="flow">The subscription flow associated with the event.</param>
    public SubscriptionFlowContextBase(SubscriptionFlow flow)
    {
        ArgumentNullException.ThrowIfNull(flow);

        Flow = flow;
    }
}
