using CrestApps.OrchardCore.Payments;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// The input a refund provider receives to execute a single refund against its authoritative API. The
/// amount is expressed in major currency units as a <see cref="decimal"/>; the provider converts it to
/// the gateway's integer minor units at its own boundary using its currency rules.
/// </summary>
public sealed class RefundPaymentContext
{
    /// <summary>
    /// The provider transaction identifier of the original payment being refunded (for example a Stripe
    /// PaymentIntent id).
    /// </summary>
    public string OriginalTransactionId { get; set; }

    /// <summary>
    /// The provider's authoritative reference for the original payment attempt, when it differs from
    /// <see cref="OriginalTransactionId"/>.
    /// </summary>
    public string OriginalProviderReference { get; set; }

    /// <summary>
    /// The gross amount to refund, including tax, in major currency units.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// The ISO-4217 currency code of the refund.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// The deterministic idempotency key the provider must honor so a retried refund never double-refunds.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// The reason for the refund, forwarded to the provider when it supports one.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// The provider mode the original payment ran in (test or live).
    /// </summary>
    public GatewayMode GatewayMode { get; set; }
}
