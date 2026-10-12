namespace CrestApps.OrchardCore.Payments;

/// <summary>
/// Provides details about a payment that was completed successfully by a payment gateway.
/// </summary>
public sealed class PaymentSucceededContext : PaymentEventContextBase
{
    /// <summary>
    /// Gets or sets the amount paid for the successful payment.
    /// </summary>
    public decimal AmountPaid { get; set; }

    /// <summary>
    /// Gets or sets the currency used for the successful payment.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the payment gateway transaction identifier for the successful payment.
    /// </summary>
    public string TransactionId { get; set; }

    /// <summary>
    /// Gets or sets subscription information related to the payment, when the payment is for a subscription.
    /// </summary>
    public SubscriptionPaymentInfo Subscription { get; set; }

    /// <summary>
    /// Gets or sets the reason the payment was created or processed.
    /// </summary>
    public PaymentReason Reason { get; set; }
}
