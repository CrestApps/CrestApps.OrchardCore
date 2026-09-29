using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Builds the schemas of the phone system provider settings, which hold data-protected secrets.
/// </summary>
/// <remarks>
/// The same settings object can reach a tenant two ways, and the two treat a secret differently. The provider's own
/// recipe step (for example <c>TelnyxSettings</c>) takes a secret in clear text, protects it before storing it, and
/// keeps the stored secret when none is supplied; it is what a deployment plan produces, and a plan never carries a
/// secret. The generic <c>Settings</c> step stores the object exactly as given, replacing what is stored, so a secret
/// there must already be protected by the destination tenant. The descriptions say which applies.
/// </remarks>
internal static class PhoneProviderSettingsSchemas
{
    /// <summary>
    /// Builds the schema of the Telnyx voice provider settings.
    /// </summary>
    /// <param name="forProviderStep">Whether the schema describes the provider's own recipe step rather than the generic <c>Settings</c> step.</param>
    public static JsonSchemaBuilder Telnyx(bool forProviderStep)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Telnyx voice provider settings. Telnyx authenticates every REST call with one tenant API key.")
            .Properties(
                ("IsEnabled", Boolean("Whether the Telnyx provider is enabled.")),
                ("ApiKey", Secret(forProviderStep, "Telnyx API key (v2), presented as a bearer token on every REST call.")),
                ("ConnectionId", NullableString("Identifier of the Telnyx Call Control (Voice API) application used to place and control calls. It is provisioned by the Connect action and belongs to one Telnyx account.")),
                ("SipConnectionId", NullableString("Optional Telnyx Credential (SIP) connection browser credentials are issued against. When empty, 'ConnectionId' is used.")),
                ("OutboundVoiceProfileId", NullableString("Optional outbound voice profile applied to outbound calls when the connection does not bind one.")),
                ("CredentialLifetimeMinutes", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Default(180).Description("Browser SIP credential lifetime, in minutes.")),
                ("DefaultOutboundCallerId", NullableString("Default E.164 caller identifier presented on outbound calls when no other caller identifier applies.")),
                ("WebhookPublicKey", Secret(forProviderStep, "Telnyx webhook Ed25519 public key (base64) used to verify inbound webhook signatures. Inbound webhooks are rejected while it is empty.")),
                ("ApiBaseUrl", NullableString("Optional override of the Telnyx REST API base address. Empty uses https://api.telnyx.com/v2/.")),
                ("SipWebSocketUrl", NullableString("SIP-over-WebSocket signaling URL browser soft phones register against. Empty uses wss://sip.telnyx.com:7443.")),
                ("SipDomain", NullableString("SIP domain browser credentials register under. Empty uses sip.telnyx.com.")),
                ("WebRtcCodecs", NullableString("Comma- or space-separated preferred WebRTC audio codecs advertised to the browser.")),
                ("WebRtcRegion", NullableString("Telnyx signaling region browser soft phones register on. Empty lets Telnyx geo-routing choose.")),
                ("IceUrls", NullableString("Comma- or space-separated STUN/TURN URLs advertised to the browser. Empty advertises a default Telnyx STUN server.")),
                ("TurnUsername", NullableString("Optional TURN user name advertised alongside 'IceUrls'.")),
                ("TurnCredential", Secret(forProviderStep, "Optional TURN credential advertised alongside 'IceUrls'.")),
                ("IceTransportPolicy", NullableString("ICE transport policy advertised to the browser, for example 'all' or 'relay'.")),
                ("EchoTestDestination", NullableString("Optional echo destination (a number or SIP URI that echoes audio back) used by the audio test and the health canary.")),
                ("OrphanedCallHandling", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Report", "EndCall").Description("What to do about a call the connection has up that the platform has no record of: record it and leave it connected, or tell the caller and hang up.")),
                ("AnsweringMachineDetection", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Premium", "Standard", "Disabled").Description("How an automated call asks the provider whether a person or a machine answered it.")),
                ("TtsVoice", NullableString("Text-to-speech voice used by spoken prompts, either 'female', 'male' or a 'Provider.Model.VoiceId' name such as 'AWS.Polly.Joanna-Neural'.")),
                ("TtsLanguage", NullableString("Language spoken prompts are said in, for example 'en-US'.")))
            .AdditionalProperties(false);

    /// <summary>
    /// Builds the schema of the Telnyx SMS provider settings.
    /// </summary>
    /// <param name="forProviderStep">Whether the schema describes the provider's own recipe step rather than the generic <c>Settings</c> step.</param>
    public static JsonSchemaBuilder TelnyxSms(bool forProviderStep)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Telnyx SMS provider settings, independent of the Telnyx voice settings.")
            .Properties(
                ("IsEnabled", Boolean("Whether the UI-configured Telnyx SMS provider is enabled.")),
                ("ApiKey", Secret(forProviderStep, "Telnyx API key (v2) presented as a bearer token on the Messaging API.")),
                ("MessagingProfileId", NullableString("Optional Telnyx messaging profile identifier used when sending.")),
                ("WebhookPublicKey", Secret(forProviderStep, "Telnyx webhook Ed25519 public key (base64) used to verify inbound messaging webhooks.")),
                ("ApiBaseUrl", NullableString("Optional override of the Telnyx REST API base address. Empty uses https://api.telnyx.com/v2/.")))
            .AdditionalProperties(false);

    /// <summary>
    /// Builds the schema of the Asterisk provider settings.
    /// </summary>
    /// <param name="forProviderStep">Whether the schema describes the provider's own recipe step rather than the generic <c>Settings</c> step.</param>
    public static JsonSchemaBuilder Asterisk(bool forProviderStep)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Asterisk provider settings for the tenant-configured ARI connection.")
            .Properties(
                ("IsEnabled", Boolean("Whether the tenant-configured Asterisk provider is enabled.")),
                ("BaseUrl", NullableString("Base URL of the Asterisk ARI endpoint.")),
                ("UserName", NullableString("ARI user name.")),
                ("Password", Secret(forProviderStep, "ARI password.")),
                ("ApplicationName", NullableString("Stasis application name used for originated channels.")),
                ("EndpointTemplate", NullableString("Optional template that turns a dialed destination into an ARI endpoint; use the {number} token for the destination.")),
                ("OutboundCallerId", NullableString("Caller identifier presented on outbound calls.")),
                ("TimeoutSeconds", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Default(30).Description("Outbound dial timeout, in seconds.")),
                ("VoicemailContext", NullableString("Dialplan context a call continues into when an agent sends an inbound offer to voicemail.")),
                ("VoicemailExtensionTemplate", NullableString("Template that resolves the voicemail dialplan extension; it may use tokens such as {voicemailRecipientUserName} or {calledAddress}.")),
                ("VoicemailPriority", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Default(1).Description("Dialplan priority used when continuing a call to voicemail.")),
                ("WebSocketUrl", NullableString("Secure WebSocket URL browser SIP user agents connect to.")),
                ("SipDomain", NullableString("SIP domain used to compose browser agent addresses of record.")),
                ("TurnUrls", NullableString("Comma- or newline-delimited STUN/TURN URLs advertised to browser agents.")),
                ("TurnSharedSecret", Secret(forProviderStep, "coturn REST shared secret used to issue time-limited TURN credentials.")),
                ("IceTransportPolicy", NullableString("ICE transport policy sent to the browser, for example 'all' or 'relay'.")),
                ("WebRtcCodecs", NullableString("Comma- or newline-delimited browser audio codec preference list, for example 'opus,g722,ulaw'.")),
                ("PjsipCredentialLifetimeMinutes", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Default(15).Description("Lifetime, in minutes, of the short-lived PJSIP credentials issued to browser agents.")),
                ("PjsipContactExpirationSeconds", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(1).Default(120).Description("PJSIP contact expiration, in seconds.")),
                ("PjsipRealtimeProviderInvariantName", NullableString("ADO.NET provider invariant name used to write PJSIP Realtime rows.")),
                ("PjsipRealtimeConnectionString", Secret(forProviderStep, "PJSIP Realtime database connection string.")),
                ("PjsipRealtimeTablePrefix", NullableString("Optional PJSIP Realtime table prefix.")))
            .AdditionalProperties(false);

    private static JsonSchemaBuilder Secret(bool forProviderStep, string description)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.String | SchemaValueType.Null)
            .Description(forProviderStep
                ? $"{description} Secret: supply it in clear text and it is data-protected before it is stored. It is never exported, and when it is omitted or empty the destination keeps the value it already stores."
                : $"{description} Secret: stored exactly as given, so it must already be data-protected by the destination tenant. Prefer the provider's own recipe step, which takes the value in clear text and protects it.");

    private static JsonSchemaBuilder NullableString(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description(description);

    private static JsonSchemaBuilder Boolean(string description)
        => new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description(description);
}
