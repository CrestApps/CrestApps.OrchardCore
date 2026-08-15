using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// A record of a single payment transaction confirmed for a checkout. Records are keyed by their
/// provider transaction id so that at-least-once provider webhooks never double-count a payment.
/// </summary>
public sealed class PaymentRecord
{
    /// <summary>
    /// The settled status of the payment.
    /// </summary>
    public PaymentStatus Status { get; set; }

    /// <summary>
    /// The amount that was charged, in major currency units.
    /// </summary>
    public double Amount { get; set; }

    /// <summary>
    /// The tax portion of <see cref="Amount"/>, determined at the time the transaction was created. Zero
    /// when taxation is disabled.
    /// </summary>
    public double TaxAmount { get; set; }

    /// <summary>
    /// The immutable tax determination captured when this transaction was created.
    /// </summary>
    public TaxSnapshot TaxSnapshot { get; set; }

    /// <summary>
    /// The ISO-4217 currency code the payment was charged in.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// The identifier of the recurring obligation this payment belongs to, when applicable.
    /// </summary>
    public string ObligationId { get; set; }

    /// <summary>
    /// The key of the payment provider that processed the payment.
    /// </summary>
    public string GatewayId { get; set; }

    /// <summary>
    /// Whether the payment was processed against the live or test gateway.
    /// </summary>
    public GatewayMode GatewayMode { get; set; }

    /// <summary>
    /// The provider transaction identifier. Used as the idempotency key for payment records.
    /// </summary>
    public string TransactionId { get; set; }
}
