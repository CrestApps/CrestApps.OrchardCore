using YesSql.Indexes;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Core.Indexes;

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
                CreatedUtc = attempt.CreatedUtc,
                Currency = attempt.Currency,
                ConfirmedAmount = attempt.ConfirmedAmount,
                ConfirmedTaxAmount = attempt.ConfirmedTaxAmount,
                ReferenceType = attempt.ReferenceType,
                ReferenceId = attempt.ReferenceId,
            });
    }
}
