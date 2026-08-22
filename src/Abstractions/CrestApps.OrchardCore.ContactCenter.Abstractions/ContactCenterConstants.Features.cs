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
        /// The identifier of the dependency-only feature that provides just the shared agent-profile directory
        /// services (the profile store, manager, index, and its storage collection). It carries no administration
        /// screens, presence, availability, reason codes, or queue concepts, so a module that only needs to resolve
        /// an operator's agent profile — such as the SMS Workspace — can depend on it without pulling in the full
        /// Agents and Work Distribution administration. Enabled automatically by the Agents feature and by any
        /// module that reuses agent identity.
        /// </summary>
        public const string AgentServices = "CrestApps.OrchardCore.ContactCenter.AgentServices";

        /// <summary>
        /// The identifier of the agents feature that adds agent profiles, skills, availability, presence, and durable agent sessions.
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
        /// The identifier of the paced Power and Progressive dialing feature.
        /// </summary>
        public const string DialerPaced = "CrestApps.OrchardCore.ContactCenter.Dialer.Paced";

        /// <summary>
        /// The identifier of the inbound voice integration feature.
        /// </summary>
        public const string Voice = "CrestApps.OrchardCore.ContactCenter.Voice";

        /// <summary>
        /// The identifier of the Contact Center bidirectional voice-media feature.
        /// </summary>
        public const string VoiceMedia = "CrestApps.OrchardCore.ContactCenter.Voice.Media";

        /// <summary>
        /// The identifier of the inbound voice front-door feature that qualifies callers through entry points.
        /// </summary>
        public const string InboundVoice = "CrestApps.OrchardCore.ContactCenter.InboundVoice";

        /// <summary>
        /// The identifier of the Contact Center recording orchestration feature.
        /// </summary>
        public const string Recording = "CrestApps.OrchardCore.ContactCenter.Recording";

        /// <summary>
        /// The identifier of the agent-assisted secure data capture feature that lets a customer enter sensitive
        /// data on a secure page so it is masked from the agent and never enters the recording.
        /// </summary>
        public const string SecureCapture = "CrestApps.OrchardCore.ContactCenter.SecureCapture";

        /// <summary>
        /// The identifier of the real-time supervisor dashboard and monitoring feature.
        /// </summary>
        public const string Supervision = "CrestApps.OrchardCore.ContactCenter.Supervision";

        /// <summary>
        /// The identifier of the shared Contact Center real-time transport feature.
        /// </summary>
        public const string RealTime = "CrestApps.OrchardCore.ContactCenter.RealTime";
    }
}
