namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Represents the payment method editor model for a subscription flow.
/// </summary>
public class SubscriptionFlowPaymentMethod
{
    /// <summary>
    /// Gets or sets the subscription flow that collects payment details.
    /// </summary>
    public SubscriptionFlow Flow { get; set; }
}
