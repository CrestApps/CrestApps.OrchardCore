using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

public sealed class SubscriptionFlowActivatingContext
{
    public SubscriptionSession Session { get; }

    public ContentItem SubscriptionContentItem { get; }

    public SubscriptionFlowActivatingContext(SubscriptionSession session, ContentItem subscriptionContentItem)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(subscriptionContentItem);

        Session = session;
        SubscriptionContentItem = subscriptionContentItem;
    }
}
