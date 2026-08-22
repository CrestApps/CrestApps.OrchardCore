namespace CrestApps.OrchardCore.Subscriptions.Models;

/// <summary>
/// Represents a request to create Stripe subscriptions for a subscription session.
/// </summary>
public class CreateSessionSubscriptionPayment
{
    /// <summary>
    /// Gets or sets the Stripe customer identifier associated with the subscription session.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the Stripe payment method identifier to use for the subscriptions.
    /// </summary>
    public string PaymentMethodId { get; set; }

    /// <summary>
    /// Gets or sets the subscription session identifier.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets metadata to attach to the created Stripe subscriptions.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; }
}
