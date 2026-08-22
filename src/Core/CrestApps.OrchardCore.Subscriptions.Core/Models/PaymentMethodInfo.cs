namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Describes the payment method used for a subscription.
/// </summary>
public sealed class PaymentMethodInfo
{
    /// <summary>
    /// Gets or sets card details for the payment method.
    /// </summary>
    public PaymentCardInfo Card { get; set; }
}
