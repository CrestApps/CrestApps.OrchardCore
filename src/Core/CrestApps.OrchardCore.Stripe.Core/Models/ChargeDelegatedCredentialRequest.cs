namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Charges, on the server, a payment credential an AI agent brought: a Stripe shared payment token or a Google Pay
/// token whose gateway is Stripe.
/// </summary>
public sealed class ChargeDelegatedCredentialRequest : StripeWriteRequest
{
    /// <summary>
    /// Gets or sets the amount to charge, in major units.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the currency of <see cref="Amount"/>.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the kind of credential, one of the checkout's delegated credential types.
    /// </summary>
    public string CredentialType { get; set; }

    /// <summary>
    /// Gets or sets the credential itself.
    /// </summary>
    public string Credential { get; set; }

    /// <summary>
    /// Gets or sets where Stripe sends a buyer back after their bank asked them to approve the payment.
    /// </summary>
    public string ReturnUrl { get; set; }

    /// <summary>
    /// Gets or sets the payment's description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the metadata kept with the payment.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; }

    /// <summary>
    /// Gets or sets the result of a 3-D Secure authentication run outside Stripe, which Stripe imports instead of
    /// running its own; the payment then fails rather than asking the buyer to approve it again.
    /// </summary>
    public StripeThreeDSecureImport ThreeDSecure { get; set; }
}
