using System.Threading;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// An optional, additive capability a <see cref="ICheckoutPaymentProvider"/> can also implement to
/// execute refunds against its authoritative API. It is intentionally separate from
/// <see cref="ICheckoutPaymentProvider"/> so existing providers that cannot refund (for example an
/// offline Pay Later commitment) are not forced to change, and so <see cref="PaymentProviderCapabilities.SupportsRefunds"/>
/// becomes a real, executable promise rather than an unimplemented flag. The checkout drives this through
/// the durable <see cref="ICheckoutRefundService"/> so every refund is recorded before the provider is
/// called and reconciled against what the provider confirms.
/// </summary>
public interface ICheckoutPaymentRefundProvider
{
    /// <summary>
    /// The stable, unique key that identifies the provider this refund capability belongs to. It matches
    /// the owning <see cref="ICheckoutPaymentProvider.Key"/>.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Refunds a settled payment. Implementations must be idempotent on
    /// <see cref="RefundPaymentContext.IdempotencyKey"/> and must return the provider's authoritative
    /// refund reference so the caller can persist it on the refund record.
    /// </summary>
    /// <param name="context">The refund context.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<PaymentRefundResult> RefundAsync(RefundPaymentContext context, CancellationToken cancellationToken = default);
}
