using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using OrchardCore;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// The default YesSql-backed <see cref="IPaymentAttemptStore"/>. This is the durable payment ledger: it
/// records every provider interaction in the tenant database so a charge is never tracked only in a
/// distributed cache that could be evicted.
/// </summary>
public sealed class PaymentAttemptStore : IPaymentAttemptStore
{
    private readonly ISession _session;
    private readonly IClock _clock;

    public PaymentAttemptStore(ISession session, IClock clock)
    {
        _session = session;
        _clock = clock;
    }

    /// <inheritdoc/>
    public Task CreateAsync(PaymentAttempt attempt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        if (string.IsNullOrEmpty(attempt.Id))
        {
            attempt.Id = IdGenerator.GenerateId();
        }

        var now = _clock.UtcNow;
        attempt.CreatedUtc = now;
        attempt.UpdatedUtc = now;

        return _session.SaveAsync(attempt, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public Task UpdateAsync(PaymentAttempt attempt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        attempt.UpdatedUtc = _clock.UtcNow;

        return _session.SaveAsync(attempt, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public Task<PaymentAttempt> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        return _session.Query<PaymentAttempt, PaymentAttemptIndex>(x => x.AttemptId == id).FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public Task<PaymentAttempt> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(idempotencyKey);

        return _session.Query<PaymentAttempt, PaymentAttemptIndex>(x => x.IdempotencyKey == idempotencyKey).FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<PaymentAttempt>> GetBySessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        return await _session.Query<PaymentAttempt, PaymentAttemptIndex>(x => x.SessionId == sessionId).ListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<PaymentAttempt>> GetPendingAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default)
    {
        return await _session.Query<PaymentAttempt, PaymentAttemptIndex>(x =>
                (x.State == PaymentAttemptState.Created || x.State == PaymentAttemptState.Pending) &&
                x.UpdatedUtc < olderThanUtc)
            .ListAsync(cancellationToken);
    }
}
