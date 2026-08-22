namespace CrestApps.OrchardCore.Telnyx.ViewModels;

/// <summary>
/// Edit view model for the Telnyx provider settings tab on the telephony settings screen.
/// </summary>
public class TelnyxSettingsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the Telnyx provider is enabled.
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
    /// Gets or sets a value indicating whether the tenant is connected to Telnyx (the connection ids have
    /// been provisioned). The connect fields and status drive whether the UI shows the "Connect" or the
    /// "Connected" experience.
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Gets or sets the resolved Telnyx Call Control connection id (managed by Connect; shown read-only).
    /// </summary>
    public string ConnectionId { get; set; }

    /// <summary>
    /// Gets or sets the resolved Telnyx Credential (SIP) connection id (managed by Connect; shown read-only).
    /// </summary>
    public string SipConnectionId { get; set; }

    /// <summary>
    /// Gets or sets the resolved outbound voice profile id (managed by Connect; shown read-only).
    /// </summary>
    public string OutboundVoiceProfileId { get; set; }

    /// <summary>
    /// Gets or sets the default outbound caller id.
    /// </summary>
    public string DefaultOutboundCallerId { get; set; }

    /// <summary>
    /// Gets or sets the Telnyx webhook Ed25519 public key. Left blank on load; enter a value only to
    /// replace the stored key.
    /// </summary>
    public string WebhookPublicKey { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a webhook public key is already stored.
    /// </summary>
    public bool HasWebhookPublicKey { get; set; }

    /// <summary>
    /// Gets or sets the browser SIP credential lifetime in minutes.
    /// </summary>
    public int CredentialLifetimeMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets the SIP-over-WebSocket signaling URL.
    /// </summary>
    public string SipWebSocketUrl { get; set; }

    /// <summary>
    /// Gets or sets the SIP domain.
    /// </summary>
    public string SipDomain { get; set; }

    /// <summary>
    /// Gets or sets the preferred WebRTC audio codecs.
    /// </summary>
    public string WebRtcCodecs { get; set; }

    /// <summary>
    /// Gets or sets the STUN/TURN ICE URLs.
    /// </summary>
    public string IceUrls { get; set; }

    /// <summary>
    /// Gets or sets the TURN username.
    /// </summary>
    public string TurnUsername { get; set; }

    /// <summary>
    /// Gets or sets the TURN credential. Left blank on load; enter a value only to replace the stored one.
    /// </summary>
    public string TurnCredential { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a TURN credential is already stored.
    /// </summary>
    public bool HasTurnCredential { get; set; }

    /// <summary>
    /// Gets or sets the ICE transport policy.
    /// </summary>
    public string IceTransportPolicy { get; set; }

    /// <summary>
    /// Gets or sets the optional REST API base address override.
    /// </summary>
    public string ApiBaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the read-only webhook endpoint path administrators register in the Telnyx portal.
    /// </summary>
    public string WebhookPath { get; set; }
}
