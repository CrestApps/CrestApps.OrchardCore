using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Payments;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Describes a refund observed at a payment gateway that must be reconciled against the durable refund
/// ledger. It is provider-neutral: a provider adapter maps its own webhook payload into this context and
/// hands it to <see cref="ICheckoutRefundReconciliationService"/>, which correlates it to a local
/// <see cref="Models.PaymentRefund"/> or quarantines it for manual review when no local request exists.
/// </summary>
public sealed class ReconcileRemoteRefundContext
{
    /// <summary>
    /// Gets or sets the provider transaction id of the original payment the refund settles against.
    /// </summary>
    public string OriginalTransactionId { get; set; }

    /// <summary>
    /// Gets or sets the provider's authoritative reference for the refund itself.
    /// </summary>
    public string ProviderRefundReference { get; set; }

    /// <summary>
    /// Gets or sets the key of the payment provider that owns the refund.
    /// </summary>
    public string ProviderKey { get; set; }

    /// <summary>
    /// Gets or sets the refunded amount reported by the gateway, in major currency units.
    /// </summary>
    public decimal RefundedAmount { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency code of the refund.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the resolved lifecycle status the gateway reports for the refund.
    /// </summary>
    public RefundStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the gateway reason for the refund, when available.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the idempotency key the refund was created with, when the gateway echoes it, so a
    /// locally initiated refund can be correlated before its provider reference was persisted.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets the gateway metadata observed on the refund, used as an additional correlation source
    /// (for example when the gateway echoes <see cref="CheckoutRefundMetadataKeys.IdempotencyKey"/> in
    /// metadata instead of on a dedicated field).
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; set; }

    /// <summary>
    /// Gets or sets the provider mode (test or live) the refund ran in.
    /// </summary>
    public GatewayMode GatewayMode { get; set; }
}
