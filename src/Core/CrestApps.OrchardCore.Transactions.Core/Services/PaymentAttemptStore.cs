using CrestApps.Core.Models;
using CrestApps.OrchardCore.Transactions.Core.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Services;
using OrchardCore.Modules;
using YesSql;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;

namespace CrestApps.OrchardCore.Transactions.Core.Services;

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
    public async Task<PageResult<PaymentAttempt>> PageAsync(int page, int pageSize, PaymentAttemptQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var records = Session.Query<PaymentAttempt, PaymentAttemptIndex>();

        if (!string.IsNullOrEmpty(query.ProviderKey))
        {
            records = records.Where(x => x.ProviderKey == query.ProviderKey);
        }

        if (query.State.HasValue)
        {
            records = records.Where(x => x.State == query.State.Value);
        }

        if (!string.IsNullOrEmpty(query.SessionId))
        {
            records = records.Where(x => x.SessionId == query.SessionId);
        }

        var count = await records.CountAsync(cancellationToken);

        var entries = await records
            .OrderByDescending(x => x.UpdatedUtc)
            .ThenByDescending(x => x.ItemId)
            .Skip((Math.Max(page, 1) - 1) * pageSize)
            .Take(pageSize)
            .ListAsync(cancellationToken);

        return new PageResult<PaymentAttempt>
        {
            Count = count,
            Entries = entries.ToArray(),
        };
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
