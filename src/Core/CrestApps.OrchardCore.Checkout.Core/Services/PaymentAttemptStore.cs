using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.YesSql.Core.Services;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// The default YesSql-backed <see cref="IPaymentAttemptStore"/>. This is the durable payment ledger: it
/// records every provider interaction in the tenant database so a charge is never tracked only in a
/// distributed cache that could be evicted.
/// </summary>
public sealed class PaymentAttemptStore : DocumentCatalog<PaymentAttempt, PaymentAttemptIndex>, IPaymentAttemptStore
{
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="PaymentAttemptStore"/> class.
    /// </summary>
    /// <param name="session">The tenant YesSql session.</param>
    /// <param name="clock">The clock used for timestamps.</param>
    public PaymentAttemptStore(
        ISession session,
        IClock clock)
        : base(session)
    {
        _clock = clock;
    }

    /// <inheritdoc/>
    public Task<PaymentAttempt> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(idempotencyKey);

        return Session.Query<PaymentAttempt, PaymentAttemptIndex>(x => x.IdempotencyKey == idempotencyKey).FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<PaymentAttempt>> GetBySessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        return await Session.Query<PaymentAttempt, PaymentAttemptIndex>(x => x.SessionId == sessionId).ListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<PaymentAttempt>> GetPendingAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default)
    {
        return await Session.Query<PaymentAttempt, PaymentAttemptIndex>(x =>
                (x.State == PaymentAttemptState.Created || x.State == PaymentAttemptState.Pending) &&
                x.UpdatedUtc < olderThanUtc)
            .ListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    protected override ValueTask SavingAsync(PaymentAttempt record)
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
