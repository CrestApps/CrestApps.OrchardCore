namespace CrestApps.OrchardCore.Payments;

/// <summary>
/// Provides details about a payment intent that succeeded at a payment gateway.
/// </summary>
public sealed class PaymentIntentSucceededContext : PaymentEventContextBase
{
    /// <summary>
    /// Gets or sets the amount associated with the successful payment intent, when available.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// Gets or sets the currency used by the successful payment intent.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the payment gateway transaction identifier for the successful payment intent.
    /// </summary>
    public string TransactionId { get; set; }
}
