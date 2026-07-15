namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Contains constant values shared across the Contact Center module set.
/// </summary>
public static class ContactCenterConstants
{
    /// <summary>
    /// The YesSql collection name used to store Contact Center documents in isolation from other modules.
    /// </summary>
    public const string CollectionName = "ContactCenter";

    /// <summary>
    /// The current schema version applied to newly published Contact Center domain events.
    /// </summary>
    public const int CurrentEventSchemaVersion = 1;

    /// <summary>
    /// Identifies a system actor for events that are not originated by an interactive user.
    /// </summary>
    public const string SystemActor = "system";

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
        /// The identifier of the Contact Center administration integration feature.
        /// </summary>
        public const string Admin = "CrestApps.OrchardCore.ContactCenter.Admin";

        /// <summary>
        /// The identifier of the agent, presence, and queue-membership feature.
        /// </summary>
        public const string Agents = "CrestApps.OrchardCore.ContactCenter.Agents";

        /// <summary>
        /// The identifier of the agent availability, presence, and durable session feature.
        /// </summary>
        public const string Availability = "CrestApps.OrchardCore.ContactCenter.Availability";

        /// <summary>
        /// The identifier of the queue and reservation feature.
        /// </summary>
        public const string Queues = "CrestApps.OrchardCore.ContactCenter.Queues";

        /// <summary>
        /// The identifier of the routing strategy and assignment orchestration feature.
        /// </summary>
        public const string Routing = "CrestApps.OrchardCore.ContactCenter.Routing";

        /// <summary>
        /// The identifier of the outbound dialer feature.
        /// </summary>
        public const string Dialer = "CrestApps.OrchardCore.ContactCenter.Dialer";

        /// <summary>
        /// The identifier of the outbound dialing compliance feature.
        /// </summary>
        public const string Compliance = "CrestApps.OrchardCore.ContactCenter.Compliance";

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

    /// <summary>
    /// Contains the well-known names of the Contact Center components that originate domain events.
    /// </summary>
    public static class Components
    {
        /// <summary>
        /// The interaction management component.
        /// </summary>
        public const string Interactions = "Interactions";

        /// <summary>
        /// The queue management component.
        /// </summary>
        public const string Queues = "Queues";

        /// <summary>
        /// The routing engine component.
        /// </summary>
        public const string Routing = "Routing";

        /// <summary>
        /// The agent and presence management component.
        /// </summary>
        public const string Agents = "Agents";

        /// <summary>
        /// The voice channel adapter component.
        /// </summary>
        public const string Voice = "Voice";

        /// <summary>
        /// The outbound dialer component.
        /// </summary>
        public const string Dialer = "Dialer";

        /// <summary>
        /// The call session management component.
        /// </summary>
        public const string CallSessions = "CallSessions";

        /// <summary>
        /// The real-time agent and supervisor experience component.
        /// </summary>
        public const string RealTime = "RealTime";
    }

    /// <summary>
    /// Contains stable metadata keys shared across Contact Center command boundaries.
    /// </summary>
    public static class CommandMetadata
    {
        /// <summary>
        /// Identifies the idempotent provider command associated with an interaction.
        /// </summary>
        public const string CommandId = "providerCommandId";

        /// <summary>
        /// Identifies the monotonic fence token for the current provider-command claim.
        /// </summary>
        public const string FenceToken = "providerCommandFence";
    }

    /// <summary>
    /// Contains the canonical Contact Center domain event type names.
    /// Names are channel-neutral and stable so they can be persisted, projected, and replayed.
    /// </summary>
    public static class Events
    {
        /// <summary>
        /// Raised when a new interaction is created.
        /// </summary>
        public const string InteractionCreated = "InteractionCreated";

        /// <summary>
        /// Raised when an interaction is linked to a CRM activity.
        /// </summary>
        public const string InteractionLinkedToActivity = "InteractionLinkedToActivity";

        /// <summary>
        /// Raised when an activity is reserved by routing, a dialer, or an agent.
        /// </summary>
        public const string ActivityReserved = "ActivityReserved";

        /// <summary>
        /// Raised when an activity assignment changes.
        /// </summary>
        public const string ActivityAssignmentChanged = "ActivityAssignmentChanged";

        /// <summary>
        /// Raised when an activity disposition is applied.
        /// </summary>
        public const string ActivityDispositionApplied = "ActivityDispositionApplied";

        /// <summary>
        /// Raised when the communication session for an interaction starts.
        /// </summary>
        public const string InteractionStarted = "InteractionStarted";

        /// <summary>
        /// Raised when an interaction is updated.
        /// </summary>
        public const string InteractionUpdated = "InteractionUpdated";

        /// <summary>
        /// Raised when an interaction is transferred.
        /// </summary>
        public const string InteractionTransferred = "InteractionTransferred";

        /// <summary>
        /// Raised when the communication session for an interaction ends.
        /// </summary>
        public const string InteractionEnded = "InteractionEnded";

        /// <summary>
        /// Raised when an interaction fails.
        /// </summary>
        public const string InteractionFailed = "InteractionFailed";

        /// <summary>
        /// Raised when routing evaluates a queued activity and its candidate agents.
        /// </summary>
        public const string RoutingDecisionMade = "RoutingDecisionMade";

        /// <summary>
        /// Raised when an activity is added to a queue.
        /// </summary>
        public const string QueueItemAdded = "QueueItemAdded";

        /// <summary>
        /// Raised when a queue item is reserved for an agent.
        /// </summary>
        public const string QueueItemReserved = "QueueItemReserved";

        /// <summary>
        /// Raised when a queue item is assigned to an agent.
        /// </summary>
        public const string QueueItemAssigned = "QueueItemAssigned";

        /// <summary>
        /// Raised when a queue item leaves the queue.
        /// </summary>
        public const string QueueItemDequeued = "QueueItemDequeued";

        /// <summary>
        /// Raised when a waiting queue item is moved to an overflow queue.
        /// </summary>
        public const string QueueItemOverflowed = "QueueItemOverflowed";

        /// <summary>
        /// Raised when an agent signs in.
        /// </summary>
        public const string AgentSignedIn = "AgentSignedIn";

        /// <summary>
        /// Raised when an agent signs out.
        /// </summary>
        public const string AgentSignedOut = "AgentSignedOut";

        /// <summary>
        /// Raised when an agent presence state changes.
        /// </summary>
        public const string AgentPresenceChanged = "AgentPresenceChanged";

        /// <summary>
        /// Raised when manager-owned agent queue or campaign entitlements change.
        /// </summary>
        public const string AgentEntitlementsChanged = "AgentEntitlementsChanged";

        /// <summary>
        /// Raised when an agent is reserved for an offer.
        /// </summary>
        public const string AgentReserved = "AgentReserved";

        /// <summary>
        /// Raised when an agent reservation is released.
        /// </summary>
        public const string AgentReleased = "AgentReleased";

        /// <summary>
        /// Raised when a dialer run starts.
        /// </summary>
        public const string DialerRunStarted = "DialerRunStarted";

        /// <summary>
        /// Raised when a dialer attempt is scheduled.
        /// </summary>
        public const string DialerAttemptScheduled = "DialerAttemptScheduled";

        /// <summary>
        /// Raised when a dialer attempt starts dialing.
        /// </summary>
        public const string DialerAttemptStarted = "DialerAttemptStarted";

        /// <summary>
        /// Raised when a dialer attempt completes.
        /// </summary>
        public const string DialerAttemptCompleted = "DialerAttemptCompleted";

        /// <summary>
        /// Raised when the outbound compliance gate suppresses a dialing attempt.
        /// </summary>
        public const string DialSuppressed = "DialSuppressed";

        /// <summary>
        /// Raised when a callback is scheduled.
        /// </summary>
        public const string CallbackScheduled = "CallbackScheduled";

        /// <summary>
        /// Raised when a due callback is promoted into outbound work.
        /// </summary>
        public const string CallbackPromoted = "CallbackPromoted";

        /// <summary>
        /// Raised when a callback is completed or canceled.
        /// </summary>
        public const string CallbackCompleted = "CallbackCompleted";

        /// <summary>
        /// Raised when a call session is created for an interaction.
        /// </summary>
        public const string CallSessionCreated = "CallSessionCreated";

        /// <summary>
        /// Raised when a call session state changes.
        /// </summary>
        public const string CallSessionUpdated = "CallSessionUpdated";

        /// <summary>
        /// Raised when a live call is connected (bridged) to an agent.
        /// </summary>
        public const string CallConnected = "CallConnected";

        /// <summary>
        /// Raised when a live call is placed on hold.
        /// </summary>
        public const string CallHeld = "CallHeld";

        /// <summary>
        /// Raised when a live call resumes from hold.
        /// </summary>
        public const string CallResumed = "CallResumed";

        /// <summary>
        /// Raised when a live call is muted.
        /// </summary>
        public const string CallMuted = "CallMuted";

        /// <summary>
        /// Raised when a live call is unmuted.
        /// </summary>
        public const string CallUnmuted = "CallUnmuted";

        /// <summary>
        /// Raised when a provider reports a conference or participant topology change for a call.
        /// </summary>
        public const string CallConferenceChanged = "CallConferenceChanged";

        /// <summary>
        /// Raised when a call session ends.
        /// </summary>
        public const string CallEnded = "CallEnded";

        /// <summary>
        /// Raised when an agent accepts an offered interaction.
        /// </summary>
        public const string OfferAccepted = "OfferAccepted";

        /// <summary>
        /// Raised when an agent declines an offered interaction.
        /// </summary>
        public const string OfferDeclined = "OfferDeclined";

        /// <summary>
        /// Raised when call recording starts.
        /// </summary>
        public const string RecordingStarted = "RecordingStarted";

        /// <summary>
        /// Raised when call recording pauses.
        /// </summary>
        public const string RecordingPaused = "RecordingPaused";

        /// <summary>
        /// Raised when call recording resumes.
        /// </summary>
        public const string RecordingResumed = "RecordingResumed";

        /// <summary>
        /// Raised when call recording stops.
        /// </summary>
        public const string RecordingStopped = "RecordingStopped";

        /// <summary>
        /// Raised when a supervisor starts monitoring, whispering, barging, or taking over a live call.
        /// </summary>
        public const string SupervisorMonitorStarted = "SupervisorMonitorStarted";
    }
}
