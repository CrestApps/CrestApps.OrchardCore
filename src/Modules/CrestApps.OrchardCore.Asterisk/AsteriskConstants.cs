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
    /// The default ARI application name used when one is not supplied explicitly.
    /// </summary>
    public const string DefaultApplicationName = "crestapps-telephony";

    /// <summary>
    /// The default outbound call timeout, in seconds.
    /// </summary>
    public const int DefaultTimeoutSeconds = 30;

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
