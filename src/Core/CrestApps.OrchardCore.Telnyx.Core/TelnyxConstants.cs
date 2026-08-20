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
    /// The name of the browser media adapter the Telnyx soft phone registers against. Telnyx ships its own
    /// WebRTC SDK (window.TelnyxWebRTC), so the provider uses the dedicated Telnyx browser media adapter,
    /// which speaks Verto to Telnyx's tuned media gateway instead of driving a raw SIP.js session.
    /// </summary>
    public const string BrowserMediaAdapterName = "telnyx-webrtc";

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
    /// The stable work-admission partition key that guards in-flight Telnyx Contact Center voice work so it can
    /// be quiesced and drained across shell reloads. It is not an Orchard feature: the Telnyx Contact Center
    /// voice adapter is now integration glue that activates whenever the Telnyx provider and Contact Center Voice
    /// are both enabled. The value is intentionally kept equal to the former feature identifier so partitioned
    /// leases and provider-command recovery survive the upgrade.
    /// </summary>
    public const string ContactCenterVoiceWorkPartition = "CrestApps.OrchardCore.Telnyx.ContactCenterVoice";

    /// <summary>
    /// Contains constants for Telnyx call recording and its secure ingestion into the encrypted media store.
    /// </summary>
    public static class Recording
    {
        /// <summary>
        /// The Telnyx webhook event type raised once a recording has finished and is available to download.
        /// </summary>
        public const string SavedEventType = "call.recording.saved";

        /// <summary>
        /// The <c>client_state</c> intent that marks a recording started for a Contact Center interaction, so the
        /// saved-recording webhook can be correlated back to the interaction that owns it.
        /// </summary>
        public const string ClientStateIntent = "cc-rec";

        /// <summary>
        /// The recording format requested from Telnyx and used as the stored media format.
        /// </summary>
        public const string Format = "mp3";

        /// <summary>
        /// The maximum number of due ingest jobs processed per background sweep.
        /// </summary>
        public const int IngestBatchSize = 25;

        /// <summary>
        /// The maximum number of ingest attempts before a recording is dead-lettered.
        /// </summary>
        public const int IngestMaxAttempts = 10;

        /// <summary>
        /// The base back-off, in seconds, between failed ingest attempts (grows exponentially).
        /// </summary>
        public const int IngestBaseBackoffSeconds = 30;

        /// <summary>
        /// The upper bound, in minutes, on the exponential ingest back-off.
        /// </summary>
        public const int IngestMaxBackoffMinutes = 60;
    }

    /// <summary>
    /// Contains the feature identifiers exposed by the Telnyx module.
    /// </summary>
    public static class Feature
    {
        /// <summary>
        /// The identifier of the Telnyx provider feature.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.Telnyx";
    }
}
