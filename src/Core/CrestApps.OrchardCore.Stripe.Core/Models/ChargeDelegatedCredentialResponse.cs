namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// What charging a delegated credential did.
/// </summary>
public sealed class ChargeDelegatedCredentialResponse
{
    /// <summary>
    /// Gets or sets a value indicating whether Stripe took the payment or is processing it.
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the buyer's bank asked the buyer to approve the payment.
    /// </summary>
    public bool RequiresAction { get; set; }

    /// <summary>
    /// Gets or sets the page the buyer approves the payment on, when <see cref="RequiresAction"/> is set.
    /// </summary>
    public string RedirectUrl { get; set; }

    /// <summary>
    /// Gets or sets the PaymentIntent's id.
    /// </summary>
    public string PaymentIntentId { get; set; }

    /// <summary>
    /// Gets or sets the PaymentIntent's status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets why the payment was refused.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the card issuer's decline code.
    /// </summary>
    public string DeclineCode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the credential itself was refused, such as an expired or used token.
    /// </summary>
    public bool InvalidCredential { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Stripe refused the payment because the bank asks the buyer to approve it
    /// and the request asked Stripe not to start that itself.
    /// </summary>
    public bool RequiresAuthentication { get; set; }

    /// <summary>
    /// Gets or sets the brand of the card, such as <c>visa</c>, when Stripe said.
    /// </summary>
    public string CardBrand { get; set; }
}
