namespace CrestApps.OrchardCore.Telnyx;

/// <summary>
/// Contains constant values used by the Telnyx telephony provider.
/// </summary>
public static class TelnyxConstants
{
    /// <summary>
    /// The technical name used to register and resolve the Telnyx provider.
    /// </summary>
    public const string ProviderTechnicalName = "Telnyx";

    /// <summary>
    /// The name of the browser media adapter the Telnyx soft phone registers against. Telnyx exposes a
    /// SIP-over-WebSocket registrar, so the provider reuses the shared SIP.js browser media adapter.
    /// </summary>
    public const string BrowserMediaAdapterName = "sipjs";

    /// <summary>
    /// The name of the data protector used to protect the Telnyx API key.
    /// </summary>
    public const string ProtectorName = "Telnyx";

    /// <summary>
    /// The name of the data protector used to protect the Telnyx webhook Ed25519 public key.
    /// </summary>
    public const string WebhookProtectorName = "Telnyx.Webhook";

    /// <summary>
    /// The default Telnyx REST API base address.
    /// </summary>
    public const string DefaultApiBaseUrl = "https://api.telnyx.com/v2/";

    /// <summary>
    /// The default Telnyx SIP-over-WebSocket signaling endpoint used by the browser WebRTC soft phone. Telnyx
    /// serves SIP over WebSocket on port 7443 (the default 443 does not accept the upgrade and closes with
    /// code 1006), so the port is explicit here.
    /// </summary>
    public const string DefaultSipWebSocketUrl = "wss://sip.telnyx.com:7443";

    /// <summary>
    /// The default Telnyx SIP domain browser credentials register under.
    /// </summary>
    public const string DefaultSipDomain = "sip.telnyx.com";

    /// <summary>
    /// The default STUN server used for browser WebRTC ICE negotiation when no TURN is configured.
    /// </summary>
    public const string DefaultStunUrl = "stun:stun.telnyx.com:3478";

    /// <summary>
    /// The HTTP header carrying the Telnyx Ed25519 webhook signature (base64).
    /// </summary>
    public const string SignatureHeaderName = "telnyx-signature-ed25519";

    /// <summary>
    /// The HTTP header carrying the Telnyx webhook signing timestamp (Unix seconds).
    /// </summary>
    public const string TimestampHeaderName = "telnyx-timestamp";

    /// <summary>
    /// The relative path of the Telnyx call-event webhook endpoint exposed by this module.
    /// </summary>
    public const string WebhookPath = "api/telnyx/webhook/call";

    /// <summary>
    /// Contains the feature identifiers exposed by the Telnyx module.
    /// </summary>
    public static class Feature
    {
        /// <summary>
        /// The identifier of the Telnyx provider feature.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.Telnyx";

        /// <summary>
        /// The identifer of the Telnyx Content Center Voice feature.
        /// </summary>
        public const string ContactCenterVoice = "CrestApps.OrchardCore.Telnyx.ContactCenterVoice";
    }
}
