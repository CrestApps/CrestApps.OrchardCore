using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// The default YesSql-backed <see cref="IPaymentRefundStore"/>. This is the durable refund ledger: it
/// records every refund in the tenant database so a refund is never tracked only in a distributed cache
/// that could be evicted, and so the total already-refunded amount can be enforced against the original
/// charge across nodes.
/// </summary>
public sealed class PaymentRefundStore : DocumentCatalog<PaymentRefund, PaymentRefundIndex>, IPaymentRefundStore
{
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentRefundStore"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session.</param>
    /// <param name="clock">The clock used for timestamps.</param>
    public PaymentRefundStore(
        ISession session,
        IClock clock)
        : base(session)
    {
        _clock = clock;
    }

    /// <inheritdoc/>
    public Task<PaymentRefund> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(idempotencyKey);

        return Session.Query<PaymentRefund, PaymentRefundIndex>(x => x.IdempotencyKey == idempotencyKey).FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<PaymentRefund> GetByProviderRefundReferenceAsync(string providerRefundReference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerRefundReference);

        return Session.Query<PaymentRefund, PaymentRefundIndex>(x => x.ProviderRefundReference == providerRefundReference).FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<PaymentRefund>> GetByOriginalTransactionAsync(string originalTransactionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(originalTransactionId);

        return await Session.Query<PaymentRefund, PaymentRefundIndex>(x => x.OriginalTransactionId == originalTransactionId).ListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<PaymentRefund>> GetBySessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        return await Session.Query<PaymentRefund, PaymentRefundIndex>(x => x.SessionId == sessionId).ListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    protected override ValueTask SavingAsync(PaymentRefund record)
    {
        var now = _clock.UtcNow;

        if (record.CreatedUtc == default)
        {
            record.CreatedUtc = now;
        }

        record.UpdatedUtc = now;

        return ValueTask.CompletedTask;
    }
}
