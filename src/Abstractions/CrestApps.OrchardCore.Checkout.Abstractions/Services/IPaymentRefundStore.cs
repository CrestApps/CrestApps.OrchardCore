using System.Threading;
using CrestApps.OrchardCore.Checkout.Models;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// The durable store for <see cref="PaymentRefund"/> records. Every refund is written here before the
/// provider is called and updated with the provider's authoritative reference afterwards, so the checkout
/// can always reconcile its own view against what really happened at the gateway. This store is backed by
/// the tenant database, never by a distributed cache, so it survives eviction, expiry, and node failure.
/// </summary>
public interface IPaymentRefundStore
{
    /// <summary>
    /// Persists a new refund.
    /// </summary>
    /// <param name="refund">The refund to persist.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task CreateAsync(PaymentRefund refund, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to an existing refund.
    /// </summary>
    /// <param name="refund">The refund to update.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task UpdateAsync(PaymentRefund refund, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a refund by its id, or <see langword="null"/> when it does not exist.
    /// </summary>
    /// <param name="id">The refund id.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<PaymentRefund> GetAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a refund by its idempotency key, or <see langword="null"/> when it does not exist. Used to
    /// resume a prior refund instead of starting a second refund for the same request.
    /// </summary>
    /// <param name="idempotencyKey">The idempotency key.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<PaymentRefund> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets every refund recorded against the supplied original payment transaction id, so the total
    /// already-refunded amount can be enforced against the original charge.
    /// </summary>
    /// <param name="originalTransactionId">The provider transaction id of the original payment.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IEnumerable<PaymentRefund>> GetByOriginalTransactionAsync(string originalTransactionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets every refund recorded for a checkout session.
    /// </summary>
    /// <param name="sessionId">The checkout session id.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IEnumerable<PaymentRefund>> GetBySessionAsync(string sessionId, CancellationToken cancellationToken = default);
}
