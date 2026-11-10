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
    /// Gets or sets a value indicating whether Stripe gave its final answer and took no money: a card error, a request
    /// it refused (such as a used token), or a PaymentIntent that ended without the money. Only then is the charge
    /// over for good, so another try is a new charge with a new idempotency key. Any other failure (a server error, a
    /// timeout, an idempotency conflict) leaves the outcome unknown, and a retry must send the same key so Stripe
    /// replays what it did instead of charging again.
    /// </summary>
    public bool Declined { get; set; }
}
