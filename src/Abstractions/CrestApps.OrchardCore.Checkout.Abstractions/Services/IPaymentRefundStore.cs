using CrestApps.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// The durable store for <see cref="PaymentRefund"/> records. Every refund is written here before the
/// provider is called and updated with the provider's authoritative reference afterwards, so the checkout
/// can always reconcile its own view against what really happened at the gateway. This store is backed by
/// the tenant database, never by a distributed cache, so it survives eviction, expiry, and node failure.
/// </summary>
public interface IPaymentRefundStore : ICatalog<PaymentRefund>
{
    /// <summary>
    /// Gets a refund by its idempotency key, or <see langword="null"/> when it does not exist. Used to
    /// resume a prior refund instead of starting a second refund for the same request.
    /// </summary>
    /// <param name="idempotencyKey">The idempotency key.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<PaymentRefund> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the refund recorded with the supplied provider refund reference, or <see langword="null"/> when
    /// none exists. Used to correlate a refund observed at the gateway back to the local ledger entry.
    /// </summary>
    /// <param name="providerRefundReference">The provider's authoritative refund reference.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<PaymentRefund> GetByProviderRefundReferenceAsync(string providerRefundReference, CancellationToken cancellationToken = default);

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
