using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Provides context for handlers that prepare a subscription session before its flow is initialized.
/// </summary>
public sealed class SubscriptionFlowInitializingContext
{
    /// <summary>
    /// Gets the subscription session being prepared.
    /// </summary>
    public SubscriptionSession Session { get; }

    /// <summary>
    /// Gets the subscription content item selected for the session.
    /// </summary>
    public ContentItem SubscriptionContentItem { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionFlowInitializingContext"/> class.
    /// </summary>
    /// <param name="session">The subscription session being prepared.</param>
    /// <param name="subscriptionContentItem">The subscription content item selected for the session.</param>
    public SubscriptionFlowInitializingContext(SubscriptionSession session, ContentItem subscriptionContentItem)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(subscriptionContentItem);

        Session = session;
        SubscriptionContentItem = subscriptionContentItem;
    }
}
