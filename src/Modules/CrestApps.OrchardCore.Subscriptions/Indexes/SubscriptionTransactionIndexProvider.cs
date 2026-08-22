using CrestApps.OrchardCore.Subscriptions.Core.Indexes;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.Entities;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Subscriptions.Indexes;

/// <summary>
/// Maps subscription session payment metadata to transaction index rows.
/// </summary>
public sealed class SubscriptionTransactionIndexProvider : IndexProvider<SubscriptionSession>
{
    /// <summary>
    /// Describes how recorded payments are projected into <see cref="SubscriptionTransactionIndex"/> rows.
    /// </summary>
    /// <param name="context">The YesSql describe context for subscription sessions.</param>
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
                    ContentType = session.ContentType,
                    Amount = payment.Amount,
                    TaxAmount = payment.TaxAmount,
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
