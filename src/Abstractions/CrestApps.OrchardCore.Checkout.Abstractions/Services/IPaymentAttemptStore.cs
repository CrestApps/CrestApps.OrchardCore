using System.Threading;
using CrestApps.OrchardCore.Checkout.Models;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// The durable store for <see cref="PaymentAttempt"/> records. Every provider interaction is written here
/// before the provider is called and updated with the provider's authoritative reference afterwards, so
/// the checkout can always reconcile its own view against what really happened at the gateway. This store
/// is backed by the tenant database, never by a distributed cache, so it survives eviction, expiry, and
/// node failure.
/// </summary>
public interface IPaymentAttemptStore
{
    /// <summary>
    /// Persists a new attempt.
    /// </summary>
    /// <param name="attempt">The attempt to persist.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task CreateAsync(PaymentAttempt attempt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to an existing attempt.
    /// </summary>
    /// <param name="attempt">The attempt to update.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task UpdateAsync(PaymentAttempt attempt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an attempt by its id, or <c>null</c> when it does not exist.
    /// </summary>
    /// <param name="id">The attempt id.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<PaymentAttempt> GetAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an attempt by its idempotency key, or <c>null</c> when it does not exist. Used to resume a
    /// prior attempt instead of starting a second charge for the same obligation.
    /// </summary>
    /// <param name="idempotencyKey">The idempotency key.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<PaymentAttempt> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets every attempt for a checkout session.
    /// </summary>
    /// <param name="sessionId">The checkout session id.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IEnumerable<PaymentAttempt>> GetBySessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets attempts that are still in a non-terminal state (<see cref="PaymentAttemptState.Created"/> or
    /// <see cref="PaymentAttemptState.Pending"/>) and are older than <paramref name="olderThanUtc"/>, so a
    /// background reconciliation sweep can verify them against the provider.
    /// </summary>
    /// <param name="olderThanUtc">Only attempts last updated before this UTC time are returned.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IEnumerable<PaymentAttempt>> GetPendingAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default);
}
