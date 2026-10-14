namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents the data required to create a Stripe SetupIntent for collecting a reusable payment method.
/// </summary>
public class CreateSetupIntentRequest : StripeWriteRequest
{
    /// <summary>
    /// Gets or sets the Stripe payment method identifier to attach to the setup intent.
    /// </summary>
    public string PaymentMethodId { get; set; }

    /// <summary>
    /// Gets or sets the Stripe customer identifier that owns the setup intent.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the metadata to store with the setup intent in Stripe.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; }
}
