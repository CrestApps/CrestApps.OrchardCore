using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Provides context while a subscription flow is being activated for a subscription content item.
/// </summary>
public sealed class SubscriptionFlowActivatingContext
{
    /// <summary>
    /// Gets the session that is being activated for the subscription flow.
    /// </summary>
    public SubscriptionSession Session { get; }

    /// <summary>
    /// Gets the subscription content item used to activate the flow.
    /// </summary>
    public ContentItem SubscriptionContentItem { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionFlowActivatingContext"/> class.
    /// </summary>
    /// <param name="session">The session that is being activated for the subscription flow.</param>
    /// <param name="subscriptionContentItem">The subscription content item used to activate the flow.</param>
    public SubscriptionFlowActivatingContext(SubscriptionSession session, ContentItem subscriptionContentItem)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(subscriptionContentItem);

        Session = session;
        SubscriptionContentItem = subscriptionContentItem;
    }
}
