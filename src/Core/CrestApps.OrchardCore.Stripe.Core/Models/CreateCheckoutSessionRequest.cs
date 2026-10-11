namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Describes a Stripe Checkout Session to create for a hosted (redirect) or embedded checkout.
/// </summary>
public sealed class CreateCheckoutSessionRequest : StripeWriteRequest
{
    /// <summary>
    /// The Stripe mode of the session. Defaults to <c>subscription</c>.
    /// </summary>
    public string Mode { get; set; } = "subscription";

    public string CustomerId { get; set; }

    public string CustomerEmail { get; set; }

    /// <summary>
    /// The recurring and/or one-time line items that make up the checkout.
    /// </summary>
    public IList<CreateCheckoutLineItem> LineItems { get; set; } = [];

    /// <summary>
    /// The absolute URL Stripe redirects the customer to after a successful hosted checkout.
    /// Required when <see cref="UiMode"/> is <c>hosted_page</c>.
    /// </summary>
    public string SuccessUrl { get; set; }

    /// <summary>
    /// The absolute URL Stripe redirects the customer to when they cancel a hosted checkout.
    /// </summary>
    public string CancelUrl { get; set; }

    /// <summary>
    /// The URL used to return to the site when using an embedded checkout.
    /// </summary>
    public string ReturnUrl { get; set; }

    /// <summary>
    /// A developer-supplied reference (for example the local subscription session id).
    /// </summary>
    public string ClientReferenceId { get; set; }

    /// <summary>
    /// Metadata stored on the Checkout Session itself.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = [];

    /// <summary>
    /// Metadata stored on the subscription that Stripe creates for a <c>subscription</c> session.
    /// This is how the local subscription session id is propagated to the invoice and subscription
    /// webhooks that finalize the purchase.
    /// </summary>
    public Dictionary<string, string> SubscriptionMetadata { get; set; } = [];

    public long? TrialPeriodDays { get; set; }

    /// <summary>
    /// The Checkout UI mode. Use <c>hosted_page</c> (default) to redirect to a Stripe-hosted page, or
    /// <c>embedded</c> to render the checkout inline using the returned client secret.
    /// </summary>
    public string UiMode { get; set; } = "hosted_page";
}
