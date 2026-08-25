namespace CrestApps.OrchardCore.Telnyx.Models;

/// <summary>
/// Represents the Telnyx provider site settings. Telnyx authenticates every REST call with a single
/// tenant API key (there is no per-user OAuth), so the settings hold one credential set.
/// </summary>
public sealed class TelnyxSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether the Telnyx provider is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets the protected Telnyx API key (v2). The value is stored encrypted using the data
    /// protection provider and is presented as a bearer token on every REST call.
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the Telnyx Call Control (Voice API) application used to place and
    /// control calls. It is the <c>connection_id</c> supplied to the calls API.
    /// </summary>
    public string ConnectionId { get; set; }

    /// <summary>
    /// Gets or sets the optional Telnyx Credential (SIP) connection identifier that browser telephony
    /// credentials are issued against for the WebRTC soft phone. When empty, <see cref="ConnectionId"/> is
    /// used. A Credential Connection is required to mint SIP username/password credentials the browser
    /// registers with.
    /// </summary>
    public string SipConnectionId { get; set; }

    /// <summary>
    /// Gets or sets the optional Telnyx outbound voice profile identifier applied to outbound calls when
    /// the connection does not already bind one.
    /// </summary>
    public string OutboundVoiceProfileId { get; set; }

    /// <summary>
    /// Gets or sets the browser SIP credential lifetime in minutes. Browser credentials are short-lived so a
    /// lost session cannot register indefinitely. Defaults to 180 (three hours): long enough that an ordinary
    /// call does not outlast its credential (renewal is deferred during a live call to avoid dropping media),
    /// short enough that an abandoned registration expires within the working day.
    /// </summary>
    public int CredentialLifetimeMinutes { get; set; } = 180;

    /// <summary>
    /// Gets or sets the default caller identifier (E.164) presented on outbound calls when no per-agent or
    /// per-request caller identifier is supplied.
    /// </summary>
    public string DefaultOutboundCallerId { get; set; }

    /// <summary>
    /// Gets or sets the protected Telnyx webhook Ed25519 public key (base64) taken from the Telnyx portal.
    /// The value is stored encrypted using the data protection provider and is used to verify the signature
    /// of every inbound webhook. Inbound webhooks are rejected when empty.
    /// </summary>
    public string WebhookPublicKey { get; set; }

    /// <summary>
    /// Gets or sets an optional internal override for the Telnyx REST API base address. When empty the
    /// default endpoint (<c>https://api.telnyx.com/v2/</c>) is used.
    /// </summary>
    public string ApiBaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the SIP-over-WebSocket signaling URL browser soft phones register against. When empty
    /// the default (<c>wss://sip.telnyx.com:7443</c>) is used.
    /// </summary>
    public string SipWebSocketUrl { get; set; }

    /// <summary>
    /// Gets or sets the SIP domain browser credentials register under. When empty the default
    /// (<c>sip.telnyx.com</c>) is used.
    /// </summary>
    public string SipDomain { get; set; }

    /// <summary>
    /// Gets or sets the comma- or space-separated preferred WebRTC audio codecs advertised to the browser.
    /// </summary>
    public string WebRtcCodecs { get; set; }

    /// <summary>
    /// Gets or sets the comma- or space-separated STUN/TURN URLs advertised to the browser for ICE
    /// negotiation. When empty a default Telnyx STUN server is advertised.
    /// </summary>
    public string IceUrls { get; set; }

    /// <summary>
    /// Gets or sets the optional TURN username advertised alongside <see cref="IceUrls"/>.
    /// </summary>
    public string TurnUsername { get; set; }

    /// <summary>
    /// Gets or sets the optional protected TURN credential advertised alongside <see cref="IceUrls"/>.
    /// </summary>
    public string TurnCredential { get; set; }

    /// <summary>
    /// Gets or sets the ICE transport policy advertised to the browser (for example <c>all</c> or
    /// <c>relay</c>).
    /// </summary>
    public string IceTransportPolicy { get; set; }

    /// <summary>
    /// Gets or sets an optional echo/loopback destination (a Telnyx number or SIP URI that echoes audio back)
    /// used by the diagnostics "Run audio test" action and the health canary to verify round-trip audio
    /// without a second person. When empty, the audio test is unavailable.
    /// </summary>
    public string EchoTestDestination { get; set; }
}
