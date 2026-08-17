using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// An in-memory <see cref="IPaymentAttemptStore"/> for exercising the durable ledger without a database.
/// </summary>
internal sealed class InMemoryPaymentAttemptStore : IPaymentAttemptStore
{
    private readonly Dictionary<string, PaymentAttempt> _attempts = new(StringComparer.Ordinal);

    public InMemoryPaymentAttemptStore(params PaymentAttempt[] seed)
    {
        foreach (var attempt in seed)
        {
            if (string.IsNullOrEmpty(attempt.ItemId))
            {
                attempt.ItemId = UniqueId.GenerateId();
            }

            _attempts[attempt.ItemId] = attempt;
        }
    }

    public ValueTask CreateAsync(PaymentAttempt attempt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(attempt.ItemId))
        {
            attempt.ItemId = UniqueId.GenerateId();
        }

        _attempts[attempt.ItemId] = attempt;

        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateAsync(PaymentAttempt attempt, CancellationToken cancellationToken = default)
    {
        _attempts[attempt.ItemId] = attempt;

        return ValueTask.CompletedTask;
    }

    public ValueTask<bool> DeleteAsync(PaymentAttempt attempt, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_attempts.Remove(attempt.ItemId));

    public ValueTask<PaymentAttempt> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_attempts.GetValueOrDefault(id));

    public ValueTask<IReadOnlyCollection<PaymentAttempt>> GetAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyCollection<PaymentAttempt>>(_attempts.Values.Where(a => ids.Contains(a.ItemId, StringComparer.Ordinal)).ToArray());

    public ValueTask<IReadOnlyCollection<PaymentAttempt>> GetAllAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyCollection<PaymentAttempt>>(_attempts.Values.ToArray());

    public ValueTask<PageResult<PaymentAttempt>> PageAsync<TQuery>(int page, int pageSize, TQuery context, CancellationToken cancellationToken = default)
        where TQuery : QueryContext
    {
        var entries = _attempts.Values.ToArray();

        return ValueTask.FromResult(new PageResult<PaymentAttempt>
        {
            Count = entries.Length,
            Entries = entries,
        });
    }

    public Task<PaymentAttempt> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => Task.FromResult(_attempts.Values.FirstOrDefault(a => a.IdempotencyKey == idempotencyKey));

    public Task<IEnumerable<PaymentAttempt>> GetBySessionAsync(string sessionId, CancellationToken cancellationToken = default)
        => Task.FromResult(_attempts.Values.Where(a => a.SessionId == sessionId));

    public Task<IEnumerable<PaymentAttempt>> GetPendingAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default)
        => Task.FromResult(_attempts.Values.Where(a =>
            (a.State == PaymentAttemptState.Created || a.State == PaymentAttemptState.Pending) &&
            a.UpdatedUtc < olderThanUtc));
}
