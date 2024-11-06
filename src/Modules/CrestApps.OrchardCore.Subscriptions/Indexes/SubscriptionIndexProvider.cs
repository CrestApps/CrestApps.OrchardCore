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
                var metadata = session.As<SubscriptionsMetadata>();

                if (metadata?.Subscriptions == null || metadata.Subscriptions.Count == 0)
                {
                    return null;
                }

                return metadata.Subscriptions.Select(subscription => new SubscriptionIndex()
                {
                    SessionId = session.SessionId,
                    OwnerId = session.OwnerId,
                    ContentType = session.ContentType,
                    ContentItemId = session.ContentItemId,
                    ContentItemVersionId = session.ContentItemVersionId,
                    StartedAt = subscription.StartedAt,
                    ExpiresAt = subscription.ExpiresAt,
                    Gateway = subscription.Gateway,
                    GatewayMode = subscription.GatewayMode,
                });
            });
    }
}
