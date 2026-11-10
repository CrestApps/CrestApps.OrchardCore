namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Confirms again, on the server, a PaymentIntent made from a credential an AI agent brought whose bank asked the buyer
/// to approve it, with the 3-D Secure result the agent got. The same PaymentIntent is confirmed, so no second payment
/// is made.
/// </summary>
public sealed class ConfirmDelegatedCredentialRequest : StripeWriteRequest
{
    /// <summary>
    /// Gets or sets the PaymentIntent to confirm.
    /// </summary>
    public string PaymentIntentId { get; set; }

    /// <summary>
    /// Gets or sets the kind of credential the PaymentIntent was made from, one of the checkout's delegated credential
    /// types.
    /// </summary>
    public string CredentialType { get; set; }

    /// <summary>
    /// Gets or sets the credential the PaymentIntent was made with. Stripe asks for the same shared payment token again
    /// when a PaymentIntent made from one is confirmed.
    /// </summary>
    public string Credential { get; set; }

    /// <summary>
    /// Gets or sets the 3-D Secure result Stripe imports.
    /// </summary>
    public StripeThreeDSecureImport ThreeDSecure { get; set; }
}
