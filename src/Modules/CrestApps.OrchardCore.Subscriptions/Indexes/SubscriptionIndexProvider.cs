using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.Entities;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Subscriptions.Indexes;

public sealed class SubscriptionIndexProvider : IndexProvider<SubscriptionSession>
{
    public override void Describe(DescribeContext<SubscriptionSession> context)
    {
        context.For<SubscriptionIndex>()
            .When(x => x.Status == SubscriptionSessionStatus.Completed)
            .Map(session =>
            {
                var subscriptions = session.As<SubscriptionCollectionMetadata>();

                if (subscriptions?.Subscriptions == null || subscriptions.Subscriptions.Count == 0)
                {
                    return null;
                }

                return subscriptions.Subscriptions.Select(x => new SubscriptionIndex()
                {
                    SessionId = session.SessionId,
                    OwnerId = session.OwnerId,
                    ContentType = session.ContentType,
                    ContentItemId = session.ContentItemId,
                    ContentItemVersionId = session.ContentItemVersionId,
                    StartedAt = x.StartedAt,
                    ExpiresAt = x.ExpiresAt,
                    Gateway = x.Gateway,
                    GatewayMode = x.GatewayMode,
                });
            });
    }
}
