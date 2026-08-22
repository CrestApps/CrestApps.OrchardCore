using CrestApps.OrchardCore.Checkout.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Checkout.Core.Indexes;

/// <summary>
/// Maps <see cref="PaymentRefund"/> documents to <see cref="PaymentRefundIndex"/> rows.
/// </summary>
public sealed class PaymentRefundIndexProvider : IndexProvider<PaymentRefund>
{
    /// <inheritdoc/>
    public override void Describe(DescribeContext<PaymentRefund> context)
    {
        context.For<PaymentRefundIndex>()
            .Map(refund => new PaymentRefundIndex
            {
                ItemId = refund.ItemId,
                SessionId = refund.SessionId,
                ProviderKey = refund.ProviderKey,
                OriginalTransactionId = refund.OriginalTransactionId,
                ProviderRefundReference = refund.ProviderRefundReference,
                IdempotencyKey = refund.IdempotencyKey,
                Status = refund.Status,
                UpdatedUtc = refund.UpdatedUtc,
            });
    }
}
