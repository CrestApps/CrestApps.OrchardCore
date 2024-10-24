using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.Entities;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Subscriptions.Indexes;

public sealed class SubscriptionTransactionIndexProvider : IndexProvider<SubscriptionSession>
{
    public override void Describe(DescribeContext<SubscriptionSession> context)
    {
        context.For<SubscriptionTransactionIndex>()
            .Map(session =>
            {
                if (!session.TryGet<PaymentsMetadata>(out var metadata) ||
                metadata.Payments == null ||
                metadata.Payments.Count == 0)
                {
                    return [];
                }

                return metadata.Payments.Values
                .Select(payment => new SubscriptionTransactionIndex()
                {
                    GatewayTransactionId = payment.TransactionId,
                    GatewayId = payment.GatewayId,
                    GatewayMode = payment.GatewayMode,
                    Amount = payment.Amount,
                    Status = payment.Status,
                    SessionId = session.SessionId,
                    CreatedUtc = session.CreatedUtc,
                    OwnerId = session.OwnerId,
                    ContentItemId = session.ContentItemId,
                    ContentItemVersionId = session.ContentItemVersionId,
                });
            });
    }
}
