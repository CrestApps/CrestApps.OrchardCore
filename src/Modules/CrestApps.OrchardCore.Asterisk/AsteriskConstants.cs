namespace CrestApps.OrchardCore.Asterisk;

/// <summary>
/// Contains constant values used by the Asterisk telephony provider.
/// </summary>
public static class AsteriskConstants
{
    /// <summary>
    /// The technical name used to register and resolve the tenant-configured Asterisk provider.
    /// </summary>
    public const string ProviderTechnicalName = "Asterisk";

    /// <summary>
    /// The technical name used to register and resolve the configuration-backed default Asterisk provider.
    /// </summary>
    public const string DefaultProviderTechnicalName = "Default Asterisk";

    /// <summary>
    /// The name of the HTTP client used for Asterisk ARI calls.
    /// </summary>
    public const string HttpClientName = "Asterisk";

    /// <summary>
    /// The name of the data protector used to protect the tenant-configured Asterisk password.
    /// </summary>
    public const string ProtectorName = "Asterisk";

    /// <summary>
    /// A suggested ARI Stasis application name shown as a placeholder in the settings UI.
    /// This value is never applied automatically; each tenant must configure an explicit,
    /// unique application name so that tenants sharing an Asterisk server never receive each
    /// other's Stasis events. When no application name is configured the provider fails closed.
    /// </summary>
    public const string SuggestedApplicationName = "crestapps-telephony";

    /// <summary>
    /// The default outbound call timeout, in seconds.
    /// </summary>
    public const int DefaultTimeoutSeconds = 30;

    /// <summary>
    /// The browser media adapter name implemented by the Telephony soft-phone client for SIP.js.
    /// </summary>
    public const string BrowserMediaAdapterName = "sipjs";

    /// <summary>
    /// The default short-lived PJSIP credential lifetime, in minutes.
    /// </summary>
    public const int DefaultPjsipCredentialLifetimeMinutes = 15;

    /// <summary>
    /// The default PJSIP contact expiration, in seconds.
    /// </summary>
    public const int DefaultPjsipContactExpirationSeconds = 120;

    /// <summary>
    /// The default browser audio codec preference list.
    /// </summary>
    public const string DefaultWebRtcCodecs = "opus,g722,ulaw";

    /// <summary>
    /// The default ICE transport policy.
    /// </summary>
    public const string DefaultIceTransportPolicy = "all";

    /// <summary>
    /// The media-session metadata key containing the host or IP address Asterisk can reach for RTP.
    /// </summary>
    public const string ExternalMediaHostMetadataKey = "externalHost";

    /// <summary>
    /// The optional media-session metadata key containing the local IP address on which Orchard binds RTP.
    /// </summary>
    public const string ExternalMediaBindAddressMetadataKey = "bindAddress";

    /// <summary>
    /// The optional media-session metadata key containing the UDP port on which Orchard binds RTP.
    /// </summary>
    public const string ExternalMediaBindPortMetadataKey = "bindPort";

    /// <summary>
    /// The channel variable used to mirror the hold state back through ARI events.
    /// </summary>
    public const string HoldStateVariableName = "CRESTAPPS_STATE_ONHOLD";

    /// <summary>
    /// The channel variable used to mirror the mute state back through ARI events.
    /// </summary>
    public const string MuteStateVariableName = "CRESTAPPS_STATE_MUTED";

    /// <summary>
    /// The channel variable used to track the provider-owned conference bridge for cleanup.
    /// </summary>
    public const string ConferenceBridgeVariableName = "CRESTAPPS_CONFERENCE_BRIDGE_ID";

    /// <summary>
    /// The channel variable and Stasis application argument stamped on channels the Contact Center
    /// originates itself (outbound calls, agent legs, supervisor legs). Its presence on a
    /// <c>StasisStart</c> event identifies a channel this tenant originated; its absence identifies a
    /// fresh inbound external call that must flow through the inbound offer path.
    /// </summary>
    public const string OriginationMarkerVariableName = "CRESTAPPS_ORIGINATED";

    /// <summary>
    /// The channel variable stamped on originated channels carrying the owning interaction identifier so
    /// realtime events can be correlated back to the originating interaction without a lookup round-trip.
    /// </summary>
    public const string InteractionChannelVariableName = "CRESTAPPS_INTERACTION_ID";

    /// <summary>
    /// The identifier prefix used for provider-owned holding bridges that park inbound callers with
    /// media (ringback or music-on-hold) until an agent accepts the offered work.
    /// </summary>
    public const string HoldingBridgePrefix = "crestapps-holding-";

    /// <summary>
    /// The shell configuration section used for the configuration-backed default provider.
    /// </summary>
    public const string DefaultConfigurationSectionPath = "CrestApps:Asterisk:Default";

    /// <summary>
    /// Contains the feature identifiers exposed by the Asterisk module.
    /// </summary>
    public static class Feature
    {
        /// <summary>
        /// The identifier of the Asterisk provider feature.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.Asterisk";

        /// <summary>
        /// The identifier of the Asterisk Contact Center voice-adapter feature.
        /// </summary>
        public const string ContactCenterVoice = "CrestApps.OrchardCore.Asterisk.ContactCenterVoice";

        /// <summary>
        /// The identifier of the Asterisk Contact Center bidirectional-media feature.
        /// </summary>
        public const string ContactCenterMedia = "CrestApps.OrchardCore.Asterisk.ContactCenterMedia";
    }
}
