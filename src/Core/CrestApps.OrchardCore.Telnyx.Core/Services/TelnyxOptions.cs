namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// Provides the Telnyx settings resolved once for the current tenant shell, with protected values already
/// unprotected.
/// </summary>
public sealed class TelnyxOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the Telnyx provider is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the unprotected Telnyx API key.
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the Telnyx Call Control (Voice API) application/connection identifier.
    /// </summary>
    public string ConnectionId { get; set; }

    /// <summary>
    /// Gets or sets the Telnyx Credential (SIP) connection identifier browser telephony credentials are
    /// issued against. Falls back to <see cref="ConnectionId"/> when not separately configured.
    /// </summary>
    public string SipConnectionId { get; set; }

    /// <summary>
    /// Gets or sets the optional Telnyx outbound voice profile identifier.
    /// </summary>
    public string OutboundVoiceProfileId { get; set; }

    /// <summary>
    /// Gets or sets the browser SIP credential lifetime in minutes.
    /// </summary>
    public int CredentialLifetimeMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets the default outbound caller identifier.
    /// </summary>
    public string DefaultOutboundCallerId { get; set; }

    /// <summary>
    /// Gets or sets the resolved REST API base address (always trailing-slash terminated).
    /// </summary>
    public string ApiBaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the SIP-over-WebSocket signaling URL browser soft phones register against.
    /// </summary>
    public string SipWebSocketUrl { get; set; }

    /// <summary>
    /// Gets or sets the SIP domain browser credentials register under.
    /// </summary>
    public string SipDomain { get; set; }

    /// <summary>
    /// Gets or sets the preferred WebRTC audio codecs advertised to the browser.
    /// </summary>
    public string WebRtcCodecs { get; set; }

    /// <summary>
    /// Gets or sets the STUN/TURN URLs advertised to the browser for ICE negotiation.
    /// </summary>
    public string IceUrls { get; set; }

    /// <summary>
    /// Gets or sets the optional TURN username advertised alongside <see cref="IceUrls"/>.
    /// </summary>
    public string TurnUsername { get; set; }

    /// <summary>
    /// Gets or sets the optional unprotected TURN credential advertised alongside <see cref="IceUrls"/>.
    /// </summary>
    public string TurnCredential { get; set; }

    /// <summary>
    /// Gets or sets the ICE transport policy advertised to the browser.
    /// </summary>
    public string IceTransportPolicy { get; set; }

    /// <summary>
    /// Gets a value indicating whether the provider has the minimum configuration required to place and
    /// control calls: an API key and a Call Control connection identifier.
    /// </summary>
    public bool IsConfigured
        => IsEnabled &&
            !string.IsNullOrWhiteSpace(ApiKey) &&
            !string.IsNullOrWhiteSpace(ConnectionId);
}
