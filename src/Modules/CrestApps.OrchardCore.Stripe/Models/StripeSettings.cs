using CrestApps.OrchardCore.Stripe.Core;

namespace CrestApps.OrchardCore.Stripe.Models;

/// <summary>
/// Stores tenant-level Stripe configuration used by checkout and webhook processing.
/// </summary>
public sealed class StripeSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether live Stripe credentials are active.
    /// </summary>
    public bool IsLive { get; set; }

    /// <summary>
    /// Gets or sets the Stripe checkout integration mode used for new checkout sessions.
    /// </summary>
    public StripeCheckoutMode CheckoutMode { get; set; } = StripeCheckoutMode.PaymentElements;

    /// <summary>
    /// Gets or sets the live Stripe publishable key.
    /// </summary>
    public string LivePublishableKey { get; set; }

    /// <summary>
    /// Gets or sets the protected live Stripe secret key.
    /// </summary>
    public string LivePrivateSecret { get; set; }

    /// <summary>
    /// Gets or sets the protected live Stripe webhook signing secret.
    /// </summary>
    public string LiveWebhookSecret { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the live Stripe account resolved when the live secret key was verified.
    /// </summary>
    public string LiveAccountId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the live webhook endpoint provisioned automatically when connecting.
    /// </summary>
    public string LiveWebhookId { get; set; }

    /// <summary>
    /// Gets or sets the test Stripe publishable key.
    /// </summary>
    public string TestPublishableKey { get; set; }

    /// <summary>
    /// Gets or sets the protected test Stripe secret key.
    /// </summary>
    public string TestPrivateSecret { get; set; }

    /// <summary>
    /// Gets or sets the protected test Stripe webhook signing secret.
    /// </summary>
    public string TestWebhookSecret { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the test Stripe account resolved when the test secret key was verified.
    /// </summary>
    public string TestAccountId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the test webhook endpoint provisioned automatically when connecting.
    /// </summary>
    public string TestWebhookId { get; set; }
}
