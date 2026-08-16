using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Payments;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// The outcome a refund provider reports after attempting a refund against its authoritative API. The
/// checkout records this on the durable <see cref="PaymentRefund"/> ledger so a refund is never assumed
/// to have succeeded unless the provider confirms it.
/// </summary>
public sealed class PaymentRefundResult
{
    /// <summary>
    /// The resulting status of the refund as reported by the provider.
    /// </summary>
    public RefundStatus Status { get; set; }

    /// <summary>
    /// The provider's authoritative reference for the refund (for example a Stripe refund id).
    /// </summary>
    public string ProviderRefundReference { get; set; }

    /// <summary>
    /// The gross amount the provider confirmed it refunded, in major currency units.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// The ISO-4217 currency code of the refund.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// The provider mode the refund ran in (test or live).
    /// </summary>
    public GatewayMode GatewayMode { get; set; }

    /// <summary>
    /// The provider failure code, when the refund failed.
    /// </summary>
    public string FailureCode { get; set; }

    /// <summary>
    /// The provider failure reason, when the refund failed.
    /// </summary>
    public string FailureReason { get; set; }

    /// <summary>
    /// Creates a successful refund result.
    /// </summary>
    /// <param name="providerRefundReference">The provider's authoritative refund reference.</param>
    /// <param name="amount">The gross amount refunded, in major currency units.</param>
    /// <param name="currency">The ISO-4217 currency code of the refund.</param>
    /// <param name="gatewayMode">The provider mode the refund ran in.</param>
    public static PaymentRefundResult Success(
        string providerRefundReference,
        decimal amount,
        string currency,
        GatewayMode gatewayMode)
        => new()
        {
            Status = RefundStatus.Succeeded,
            ProviderRefundReference = providerRefundReference,
            Amount = amount,
            Currency = currency,
            GatewayMode = gatewayMode,
        };

    /// <summary>
    /// Creates a pending refund result for a provider that accepted the refund but has not yet confirmed
    /// it (for example an asynchronous bank refund).
    /// </summary>
    /// <param name="providerRefundReference">The provider's authoritative refund reference.</param>
    /// <param name="gatewayMode">The provider mode the refund ran in.</param>
    public static PaymentRefundResult Pending(string providerRefundReference, GatewayMode gatewayMode)
        => new()
        {
            Status = RefundStatus.Pending,
            ProviderRefundReference = providerRefundReference,
            GatewayMode = gatewayMode,
        };

    /// <summary>
    /// Creates a failed refund result.
    /// </summary>
    /// <param name="failureCode">The provider failure code.</param>
    /// <param name="failureReason">The provider failure reason.</param>
    public static PaymentRefundResult Failed(string failureCode, string failureReason)
        => new()
        {
            Status = RefundStatus.Failed,
            FailureCode = failureCode,
            FailureReason = failureReason,
        };
}
