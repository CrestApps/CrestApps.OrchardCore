namespace CrestApps.OrchardCore.Telnyx.Models;

/// <summary>
/// The UI-driven site settings for the Telnyx SMS provider. Self-contained (independent of the Telnyx voice
/// settings) so a tenant can run Telnyx SMS without Telnyx voice, mirroring OrchardCore's Twilio settings.
/// The API key and webhook public key are stored encrypted via the data-protection provider.
/// </summary>
public sealed class TelnyxSmsSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether the tenant has enabled the UI-configured Telnyx SMS provider.
    /// The provider only becomes selectable once this is enabled and the credentials validate (or the
    /// configuration-driven default supplies them).
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the protected Telnyx API key (v2) presented as a bearer token on the Messaging API.
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the optional Telnyx messaging profile identifier used when sending.
    /// </summary>
    public string MessagingProfileId { get; set; }

    /// <summary>
    /// Gets or sets the protected Telnyx webhook Ed25519 public key (base64) used to verify inbound messaging
    /// webhooks.
    /// </summary>
    public string WebhookPublicKey { get; set; }

    /// <summary>
    /// Gets or sets an optional override for the Telnyx REST API base address. When empty the default
    /// (<c>https://api.telnyx.com/v2/</c>) is used.
    /// </summary>
    public string ApiBaseUrl { get; set; }
}
