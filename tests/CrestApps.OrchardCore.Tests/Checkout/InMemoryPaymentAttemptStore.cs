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
            _attempts[attempt.Id] = attempt;
        }
    }

    public Task CreateAsync(PaymentAttempt attempt, CancellationToken cancellationToken = default)
    {
        _attempts[attempt.Id] = attempt;

        return Task.CompletedTask;
    }

    public Task UpdateAsync(PaymentAttempt attempt, CancellationToken cancellationToken = default)
    {
        _attempts[attempt.Id] = attempt;

        return Task.CompletedTask;
    }

    public Task<PaymentAttempt> GetAsync(string id, CancellationToken cancellationToken = default)
        => Task.FromResult(_attempts.GetValueOrDefault(id));

    public Task<PaymentAttempt> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        => Task.FromResult(_attempts.Values.FirstOrDefault(a => a.IdempotencyKey == idempotencyKey));

    public Task<IEnumerable<PaymentAttempt>> GetBySessionAsync(string sessionId, CancellationToken cancellationToken = default)
        => Task.FromResult(_attempts.Values.Where(a => a.SessionId == sessionId));

    public Task<IEnumerable<PaymentAttempt>> GetPendingAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default)
        => Task.FromResult(_attempts.Values.Where(a =>
            (a.State == PaymentAttemptState.Created || a.State == PaymentAttemptState.Pending) &&
            a.UpdatedUtc < olderThanUtc));
}
