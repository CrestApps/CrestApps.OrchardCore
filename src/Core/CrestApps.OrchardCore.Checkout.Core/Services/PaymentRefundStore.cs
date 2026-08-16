using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using OrchardCore;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// The default YesSql-backed <see cref="IPaymentRefundStore"/>. This is the durable refund ledger: it
/// records every refund in the tenant database so a refund is never tracked only in a distributed cache
/// that could be evicted, and so the total already-refunded amount can be enforced against the original
/// charge across nodes.
/// </summary>
public sealed class PaymentRefundStore : IPaymentRefundStore
{
    private readonly ISession _session;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentRefundStore"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session.</param>
    /// <param name="clock">The clock used for timestamps.</param>
    public PaymentRefundStore(ISession session, IClock clock)
    {
        _session = session;
        _clock = clock;
    }

    /// <inheritdoc/>
    public Task CreateAsync(PaymentRefund refund, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(refund);

        if (string.IsNullOrEmpty(refund.Id))
        {
            refund.Id = IdGenerator.GenerateId();
        }

        var now = _clock.UtcNow;
        refund.CreatedUtc = now;
        refund.UpdatedUtc = now;

        return _session.SaveAsync(refund, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public Task UpdateAsync(PaymentRefund refund, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(refund);

        refund.UpdatedUtc = _clock.UtcNow;

        return _session.SaveAsync(refund, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public Task<PaymentRefund> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        return _session.Query<PaymentRefund, PaymentRefundIndex>(x => x.RefundId == id).FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<PaymentRefund> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(idempotencyKey);

        return _session.Query<PaymentRefund, PaymentRefundIndex>(x => x.IdempotencyKey == idempotencyKey).FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<PaymentRefund>> GetByOriginalTransactionAsync(string originalTransactionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(originalTransactionId);

        return await _session.Query<PaymentRefund, PaymentRefundIndex>(x => x.OriginalTransactionId == originalTransactionId).ListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<PaymentRefund>> GetBySessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        return await _session.Query<PaymentRefund, PaymentRefundIndex>(x => x.SessionId == sessionId).ListAsync(cancellationToken);
    }
}
