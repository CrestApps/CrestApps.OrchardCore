namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The resolved, plaintext Telnyx SMS provider options, produced by merging the configuration-driven defaults
/// (appsettings) with the UI site settings (which override). Consumed by the provider and webhook via
/// <see cref="Microsoft.Extensions.Options.IOptionsMonitor{TOptions}"/>.
/// </summary>
public sealed class TelnyxSmsOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the Telnyx SMS provider is configured and enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the unprotected Telnyx API key.
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the optional Telnyx messaging profile identifier used when sending.
    /// </summary>
    public string MessagingProfileId { get; set; }

    /// <summary>
    /// Gets or sets the unprotected Telnyx webhook Ed25519 public key (base64).
    /// </summary>
    public string WebhookPublicKey { get; set; }

    /// <summary>
    /// Gets or sets the resolved Telnyx REST API base address (always ends with a trailing slash).
    /// </summary>
    public string ApiBaseUrl { get; set; }

    /// <summary>
    /// Gets a value indicating whether the options carry the minimum required to send (enabled + API key).
    /// </summary>
    public bool IsValid => IsEnabled && !string.IsNullOrWhiteSpace(ApiKey);
}
