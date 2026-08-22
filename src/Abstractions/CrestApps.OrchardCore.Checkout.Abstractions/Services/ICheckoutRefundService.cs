using System.Threading;
using CrestApps.OrchardCore.Checkout.Models;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Orchestrates refunds against the durable <see cref="PaymentRefund"/> ledger. It is the single
/// authoritative entry point for issuing a refund: it validates the remaining refundable amount, derives
/// the refunded tax from the original payment's immutable tax snapshot (never from current rules),
/// records the refund before calling the provider, serializes concurrent refunds of the same payment with
/// a distributed lock so two nodes can never over-refund, and reconciles the ledger against what the
/// provider confirms. Callers must not talk to a refund provider directly.
/// </summary>
public interface ICheckoutRefundService
{
    /// <summary>
    /// Requests a refund of a settled payment. The refund is persisted as
    /// <see cref="RefundStatus.Requested"/> before the provider is called and updated with the provider's
    /// confirmed outcome. When the owning provider has no executable refund operation the refund is
    /// recorded as <see cref="RefundStatus.PendingManualReview"/> for an operator to settle.
    /// </summary>
    /// <param name="context">The refund request context.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The durable refund record reflecting the outcome.</returns>
    Task<PaymentRefund> RequestRefundAsync(RequestPaymentRefundContext context, CancellationToken cancellationToken = default);
}
