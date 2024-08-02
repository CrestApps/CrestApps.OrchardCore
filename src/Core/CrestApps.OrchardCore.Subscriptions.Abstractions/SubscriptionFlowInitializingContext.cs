using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public sealed class SubscriptionFlowInitializingContext
{
    public SubscriptionSession Session { get; }

    public ContentItem SubscriptionContentItem { get; }

    public SubscriptionFlowInitializingContext(SubscriptionSession session, ContentItem subscriptionContentItem)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(subscriptionContentItem);

        Session = session;
        SubscriptionContentItem = subscriptionContentItem;
    }
}
