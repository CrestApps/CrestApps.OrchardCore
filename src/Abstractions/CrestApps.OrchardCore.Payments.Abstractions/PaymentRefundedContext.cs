namespace CrestApps.OrchardCore.Payments;

/// <summary>
/// Provides details about a refund observed at a payment gateway, whether it was initiated by this
/// application or created out-of-band (for example from the gateway dashboard). It is the provider-neutral
/// notification used to reconcile the durable refund ledger against what really happened at the gateway.
/// The gateway is authoritative for the refund status and reference; consumers must not fabricate a refund
/// result from anything other than the provider's confirmed values.
/// </summary>
public sealed class PaymentRefundedContext : PaymentEventContextBase
{
    /// <summary>
    /// Gets or sets the provider transaction id of the original payment that was refunded (for a card
    /// payment this is the payment intent or charge id the refund settles against).
    /// </summary>
    public string OriginalTransactionId { get; set; }

    /// <summary>
    /// Gets or sets the provider's authoritative reference for the refund itself.
    /// </summary>
    public string ProviderRefundReference { get; set; }

    /// <summary>
    /// Gets or sets the refunded amount reported by the gateway.
    /// </summary>
    public decimal RefundedAmount { get; set; }

    /// <summary>
    /// Gets or sets the currency of the refund.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the gateway lifecycle status of the refund (for example <c>succeeded</c>, <c>pending</c>,
    /// <c>failed</c>, or <c>canceled</c>), used to reconcile the local ledger entry.
    /// </summary>
    public string RefundStatus { get; set; }

    /// <summary>
    /// Gets or sets the reason reported by the gateway for the refund, when available.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the idempotency key the refund was created with, when the gateway echoes it, so a
    /// locally initiated refund can be correlated even before its provider reference was persisted.
    /// </summary>
    public string IdempotencyKey { get; set; }
}
