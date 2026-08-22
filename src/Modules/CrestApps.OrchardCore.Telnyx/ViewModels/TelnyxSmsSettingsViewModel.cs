namespace CrestApps.OrchardCore.Telnyx.ViewModels;

/// <summary>
/// The edit view model for the UI-driven Telnyx SMS provider settings on the SMS settings screen.
/// </summary>
public class TelnyxSmsSettingsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the Telnyx SMS provider is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the Telnyx API key. Left blank on load; enter a value only to replace the stored key.
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an API key is already stored.
    /// </summary>
    public bool HasApiKey { get; set; }

    /// <summary>
    /// Gets or sets the optional Telnyx messaging profile identifier used when sending.
    /// </summary>
    public string MessagingProfileId { get; set; }

    /// <summary>
    /// Gets or sets the Telnyx webhook Ed25519 public key. Left blank on load; enter a value only to replace it.
    /// </summary>
    public string WebhookPublicKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a webhook public key is already stored.
    /// </summary>
    public bool HasWebhookPublicKey { get; set; }

    /// <summary>
    /// Gets or sets the optional REST API base address override.
    /// </summary>
    public string ApiBaseUrl { get; set; }
}
