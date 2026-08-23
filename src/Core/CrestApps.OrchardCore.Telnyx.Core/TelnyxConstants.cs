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
    /// The stable work-admission partition key that guards in-flight Telnyx Contact Center bidirectional media work
    /// so it can be quiesced and drained across shell reloads. Like the voice partition it is not an Orchard feature:
    /// the Telnyx media adapter is integration glue that activates whenever the Telnyx provider and Contact Center
    /// Voice Media are both enabled.
    /// </summary>
    public const string ContactCenterMediaWorkPartition = "CrestApps.OrchardCore.Telnyx.ContactCenterMedia";

    /// <summary>
    /// The relative path of the Telnyx media-streaming WebSocket endpoint exposed by this module. Telnyx dials this
    /// endpoint after a <c>streaming_start</c> command, so it must be reachable at the tenant's public base URL.
    /// </summary>
    public const string MediaStreamPath = "api/telnyx/media/stream";

    /// <summary>
    /// The media-session metadata key that overrides the public base URL used to build the Telnyx media-stream
    /// <c>stream_url</c>. When absent, the tenant site base URL is used.
    /// </summary>
    public const string MediaStreamPublicUrlMetadataKey = "mediaStreamPublicUrl";

    /// <summary>
    /// Contains constants for Telnyx bidirectional media streaming over WebSockets, the equivalent of Asterisk ARI
    /// External Media used by the Contact Center voice-media provider boundary.
    /// </summary>
    public static class MediaStreaming
    {
        /// <summary>
        /// The Telnyx codec used for both the streamed call audio and the injected bidirectional audio. G.711 mu-law
        /// at 8 kHz matches the format the Contact Center voice-media session advertises.
        /// </summary>
        public const string Codec = "PCMU";

        /// <summary>
        /// The call track streamed to the WebSocket. The inbound track carries the audio arriving from the far end.
        /// </summary>
        public const string Track = "inbound_track";

        /// <summary>
        /// The bidirectional streaming mode. RTP mode delivers raw base64 codec payloads in both directions, which the
        /// session exchanges directly as <see cref="ContactCenter.Models.ContactCenterVoiceMediaFrame"/> data.
        /// </summary>
        public const string BidirectionalMode = "rtp";

        /// <summary>
        /// The call legs the injected bidirectional audio is played into. "self" plays it into the leg the stream is
        /// attached to, so audio written to the session is heard by the party on that call.
        /// </summary>
        public const string BidirectionalTargetLegs = "self";
    }

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
        /// The <c>client_state</c> intent set on the voicemail greeting (the <c>speak</c> or <c>playback_start</c>
        /// that plays "leave your message"). It is echoed on the greeting's <c>call.speak.ended</c> /
        /// <c>call.playback.ended</c> webhook, which is the signal to start the beep-and-record only after the
        /// greeting has finished playing, so the greeting is never captured inside the caller's message.
        /// </summary>
        public const string VoicemailGreetingClientStateIntent = "cc-vmg";

        /// <summary>
        /// The Telnyx webhook event type raised once a spoken (text-to-speech) prompt has finished playing.
        /// </summary>
        public const string SpeakEndedEventType = "call.speak.ended";

        /// <summary>
        /// The Telnyx webhook event type raised once an audio playback has finished playing.
        /// </summary>
        public const string PlaybackEndedEventType = "call.playback.ended";

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
    /// The relative path of the Telnyx messaging (SMS/MMS) webhook endpoint exposed by the Telnyx SMS feature.
    /// It receives inbound messages and outbound delivery receipts, and is registered against a Telnyx
    /// messaging profile in the Telnyx portal.
    /// </summary>
    public const string SmsWebhookPath = "api/telnyx/webhook/sms";

    /// <summary>
    /// The appsettings/configuration section that supplies the Telnyx SMS provider's default (config-driven)
    /// credentials, mirroring OrchardCore's <c>OrchardCore_Sms_Twilio</c> convention.
    /// </summary>
    public const string SmsConfigurationSection = "OrchardCore_Sms_Telnyx";

    /// <summary>
    /// The data protector used to protect the Telnyx SMS API key stored in the UI settings.
    /// </summary>
    public const string SmsApiKeyProtectorName = "Telnyx.Sms.ApiKey";

    /// <summary>
    /// The data protector used to protect the Telnyx SMS webhook public key stored in the UI settings.
    /// </summary>
    public const string SmsWebhookProtectorName = "Telnyx.Sms.Webhook";

    /// <summary>
    /// The relative path (against the v2 API base) of the Telnyx Messaging API used to send outbound messages.
    /// </summary>
    public const string MessagesPath = "messages";

    /// <summary>
    /// Contains the Telnyx messaging (SMS/MMS) webhook event types.
    /// </summary>
    public static class SmsEvents
    {
        /// <summary>
        /// Raised when an inbound message is received.
        /// </summary>
        public const string MessageReceived = "message.received";

        /// <summary>
        /// Raised as an outbound message progresses toward the carrier.
        /// </summary>
        public const string MessageSent = "message.sent";

        /// <summary>
        /// Raised when an outbound message reaches a terminal delivery state.
        /// </summary>
        public const string MessageFinalized = "message.finalized";
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

        /// <summary>
        /// The identifier of the Telnyx SMS provider feature: the outbound SMS/MMS provider and the inbound
        /// and delivery-receipt messaging webhook.
        /// </summary>
        public const string Sms = "CrestApps.OrchardCore.Telnyx.Sms";
    }
}
