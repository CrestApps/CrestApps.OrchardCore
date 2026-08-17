using CrestApps.OrchardCore.Checkout.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Checkout.Core.Indexes;

/// <summary>
/// Maps <see cref="PaymentAttempt"/> documents to <see cref="PaymentAttemptIndex"/> rows.
/// </summary>
public sealed class PaymentAttemptIndexProvider : IndexProvider<PaymentAttempt>
{
    /// <inheritdoc/>
    public override void Describe(DescribeContext<PaymentAttempt> context)
    {
        context.For<PaymentAttemptIndex>()
            .Map(attempt => new PaymentAttemptIndex
            {
                ItemId = attempt.ItemId,
                SessionId = attempt.SessionId,
                ProviderKey = attempt.ProviderKey,
                ObligationId = attempt.ObligationId,
                IdempotencyKey = attempt.IdempotencyKey,
                ProviderReference = attempt.ProviderReference,
                State = attempt.State,
                UpdatedUtc = attempt.UpdatedUtc,
            });
    }
}
