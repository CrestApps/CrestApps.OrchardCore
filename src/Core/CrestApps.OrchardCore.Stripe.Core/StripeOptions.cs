namespace CrestApps.OrchardCore.Stripe.Core;

/// <summary>
/// Represents configuration options for the Stripe integration.
/// </summary>
public sealed class StripeOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the integration uses Stripe live mode.
    /// </summary>
    public bool IsLive { get; set; }

    /// <summary>
    /// Gets or sets the Stripe publishable key used by client-side Stripe.js components.
    /// </summary>
    public string PublishableKey { get; set; }

    /// <summary>
    /// Gets or sets the Stripe secret API key used by server-side Stripe services.
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the Stripe webhook signing secret used to verify incoming webhook events.
    /// </summary>
    public string WebhookSecret { get; set; }

    /// <summary>
    /// The Stripe integration model used to collect payment. Defaults to
    /// <see cref="StripeCheckoutMode.PaymentElements"/> for backward compatibility.
    /// </summary>
    public StripeCheckoutMode CheckoutMode { get; set; } = StripeCheckoutMode.PaymentElements;

    /// <summary>
    /// Gets a value indicating whether the integration has resolved a usable Stripe secret key.
    /// </summary>
    public bool IsConfigured
        => !string.IsNullOrEmpty(ApiKey);
}
