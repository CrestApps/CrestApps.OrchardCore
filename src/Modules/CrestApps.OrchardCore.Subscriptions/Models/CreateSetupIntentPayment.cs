namespace CrestApps.OrchardCore.Subscriptions.Models;

/// <summary>
/// Represents a request to create a Stripe SetupIntent for a subscription payment method.
/// </summary>
public class CreateSetupIntentPayment
{
    /// <summary>
    /// Gets or sets the subscription session identifier.
    /// </summary>
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets the Stripe payment method identifier to attach to the customer.
    /// </summary>
    public string PaymentMethodId { get; set; }

    /// <summary>
    /// Gets or sets metadata to attach to the Stripe customer and SetupIntent.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; }
}
