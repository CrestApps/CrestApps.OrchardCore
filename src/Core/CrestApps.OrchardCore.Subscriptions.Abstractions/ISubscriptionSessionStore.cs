
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Subscriptions;

public interface ISubscriptionSessionStore
{
    Task<SubscriptionSession> GetAsync(string sessionId, SubscriptionSessionStatus status);
    Task<SubscriptionSession> GetOrNewAsync(ContentItem subscriptionContentItem);
    Task<SubscriptionSession> NewAsync(ContentItem subscriptionContentItem);
    Task SaveAsync(SubscriptionSession session);
}