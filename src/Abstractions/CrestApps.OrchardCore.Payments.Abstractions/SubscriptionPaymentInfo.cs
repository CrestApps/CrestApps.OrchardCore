namespace CrestApps.OrchardCore.Payments;

/// <summary>
/// Provides subscription-specific payment information for payment event contexts.
/// </summary>
public sealed class SubscriptionPaymentInfo : PaymentEventContextBase
{
    /// <summary>
    /// Gets or sets the payment gateway identifier of the subscription associated with the payment.
    /// </summary>
    public string SubscriptionId { get; set; }
}
