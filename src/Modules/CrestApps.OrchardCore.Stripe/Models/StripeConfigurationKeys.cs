namespace CrestApps.OrchardCore.Stripe.Models;

/// <summary>
/// Holds the Stripe keys of one mode (test or live) read from the application configuration.
/// </summary>
public sealed class StripeConfigurationKeys
{
    /// <summary>
    /// Gets or sets the publishable key (<c>pk_test_</c> or <c>pk_live_</c>) the card form uses.
    /// </summary>
    public string PublishableKey { get; set; }

    /// <summary>
    /// Gets or sets the secret key (<c>sk_test_</c> or <c>sk_live_</c>) the server uses to call Stripe.
    /// </summary>
    public string SecretKey { get; set; }

    /// <summary>
    /// Gets or sets the webhook signing secret (<c>whsec_</c>) used to verify the events Stripe sends.
    /// </summary>
    public string WebhookSecret { get; set; }

    /// <summary>
    /// Gets a value indicating whether the publishable key is configured.
    /// </summary>
    public bool HasPublishableKey
        => !string.IsNullOrWhiteSpace(PublishableKey);

    /// <summary>
    /// Gets a value indicating whether the secret key is configured.
    /// </summary>
    public bool HasSecretKey
        => !string.IsNullOrWhiteSpace(SecretKey);

    /// <summary>
    /// Gets a value indicating whether the webhook signing secret is configured.
    /// </summary>
    public bool HasWebhookSecret
        => !string.IsNullOrWhiteSpace(WebhookSecret);

    /// <summary>
    /// Gets a value indicating whether any key of the mode is configured.
    /// </summary>
    public bool HasAny
        => HasPublishableKey || HasSecretKey || HasWebhookSecret;
}
