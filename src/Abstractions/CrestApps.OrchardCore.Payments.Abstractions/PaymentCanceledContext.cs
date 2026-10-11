namespace CrestApps.OrchardCore.Payments;

/// <summary>
/// Provides details about a payment that was canceled at a payment gateway (for example an abandoned or
/// expired payment intent), so features can release reservations and return an obligation to an unpaid
/// state without treating the cancellation as a failure.
/// </summary>
public sealed class PaymentCanceledContext : PaymentEventContextBase
{
    /// <summary>
    /// Gets or sets the payment gateway transaction identifier for the canceled payment.
    /// </summary>
    public string TransactionId { get; set; }

    /// <summary>
    /// Gets or sets the currency used by the canceled payment.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the gateway cancellation reason, when available.
    /// </summary>
    public string Reason { get; set; }
}
