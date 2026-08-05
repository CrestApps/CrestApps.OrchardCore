namespace CrestApps.OrchardCore.ContactCenter;

public static partial class ContactCenterConstants
{
    /// <summary>
    /// Contains the feature identifiers exposed by the Contact Center module set.
    /// </summary>
    public static class Feature
    {
        /// <summary>
        /// The identifier of the base Contact Center feature.
        /// </summary>
        public const string Area = "CrestApps.OrchardCore.ContactCenter";

        /// <summary>
        /// The identifier of the workforce feature that adds agent profiles, skills, availability, presence, and durable agent sessions.
        /// </summary>
        public const string Agents = "CrestApps.OrchardCore.ContactCenter.Agents";

        /// <summary>
        /// The identifier of the work-distribution feature that adds queues, reservations, and routing strategies.
        /// </summary>
        public const string Queues = "CrestApps.OrchardCore.ContactCenter.Queues";

        /// <summary>
        /// The identifier of the outbound dialer feature.
        /// </summary>
        public const string Dialer = "CrestApps.OrchardCore.ContactCenter.Dialer";

        /// <summary>
        /// The identifier of automated power and progressive dialing feature.
        /// </summary>
        public const string DialerAutomated = "CrestApps.OrchardCore.ContactCenter.Dialer.Automated";

        /// <summary>
        /// The identifier of the inbound voice integration feature.
        /// </summary>
        public const string Voice = "CrestApps.OrchardCore.ContactCenter.Voice";

        /// <summary>
        /// The identifier of the Contact Center bidirectional voice-media feature.
        /// </summary>
        public const string VoiceMedia = "CrestApps.OrchardCore.ContactCenter.Voice.Media";

        /// <summary>
        /// The identifier of the inbound voice entry-point qualification feature.
        /// </summary>
        public const string EntryPoints = "CrestApps.OrchardCore.ContactCenter.EntryPoints";

        /// <summary>
        /// The identifier of the Contact Center recording orchestration feature.
        /// </summary>
        public const string Recording = "CrestApps.OrchardCore.ContactCenter.Recording";

        /// <summary>
        /// The identifier of the Contact Center soft-phone integration feature.
        /// </summary>
        public const string VoiceSoftPhone = "CrestApps.OrchardCore.ContactCenter.Voice.SoftPhone";

        /// <summary>
        /// The identifier of the CRM-integrated agent desktop feature.
        /// </summary>
        public const string AgentDesktop = "CrestApps.OrchardCore.ContactCenter.AgentDesktop";

        /// <summary>
        /// The identifier of the real-time supervisor dashboard and monitoring feature.
        /// </summary>
        public const string Supervision = "CrestApps.OrchardCore.ContactCenter.Supervision";

        /// <summary>
        /// The identifier of the shared Contact Center real-time transport feature.
        /// </summary>
        public const string RealTime = "CrestApps.OrchardCore.ContactCenter.RealTime";

        /// <summary>
        /// The identifier of the reporting and analytics feature.
        /// </summary>
        public const string Analytics = "CrestApps.OrchardCore.ContactCenter.Analytics";

        /// <summary>
        /// The identifier of the Orchard Core Workflows integration feature.
        /// </summary>
        public const string Workflows = "CrestApps.OrchardCore.ContactCenter.Workflows";
    }
}
