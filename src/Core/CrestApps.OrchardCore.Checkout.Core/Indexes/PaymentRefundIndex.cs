using CrestApps.OrchardCore.Checkout.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Checkout.Core.Indexes;

/// <summary>
/// The queryable projection of a <see cref="PaymentRefund"/>, the durable refund ledger.
/// </summary>
public sealed class PaymentRefundIndex : MapIndex
{
    /// <summary>
    /// The refund id.
    /// </summary>
    public string RefundId { get; set; }

    /// <summary>
    /// The checkout session id the original payment belongs to.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// The payment provider key that owns the refund.
    /// </summary>
    public string ProviderKey { get; set; }

    /// <summary>
    /// The provider transaction id of the original payment being refunded.
    /// </summary>
    public string OriginalTransactionId { get; set; }

    /// <summary>
    /// The provider's authoritative reference for this refund.
    /// </summary>
    public string ProviderRefundReference { get; set; }

    /// <summary>
    /// The idempotency key used with the provider.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// The lifecycle state of the refund.
    /// </summary>
    public RefundStatus Status { get; set; }

    /// <summary>
    /// The UTC time the refund was last updated.
    /// </summary>
    public DateTime UpdatedUtc { get; set; }
}
