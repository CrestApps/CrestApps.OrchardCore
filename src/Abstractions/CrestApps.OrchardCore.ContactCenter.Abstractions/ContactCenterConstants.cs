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
    /// The stable, versioned identifier of the daily event-count metrics projection. It namespaces the
    /// projection's deduplication markers and replay checkpoint, so its value must never change for a given
    /// projection logic version.
    /// </summary>
    public const string MetricsProjectionHandlerId = "ContactCenter/MetricsProjection/v1";

    /// <summary>
    /// The projection logic version of the daily event-count metrics projection. Bumping it forces a full
    /// replay because the stored checkpoint version no longer matches.
    /// </summary>
    public const int MetricsProjectionVersion = 1;

    /// <summary>
    /// Identifies a system actor for events that are not originated by an interactive user.
    /// </summary>
    public const string SystemActor = "system";

    /// <summary>
    /// Contains the identifiers used to register and select the Contact Center operational health checks.
    /// The readiness endpoint selects checks by <see cref="HealthChecks.ReadyTag"/>, so a registration that
    /// omits the tag silently disappears from readiness. Both sides must reference these constants.
    /// </summary>
    public static class HealthChecks
    {
        /// <summary>
        /// The tag applied to every Contact Center health check, used to distinguish them from checks
        /// contributed by other modules.
        /// </summary>
        public const string AreaTag = "contactcenter";

        /// <summary>
        /// The tag applied to node-local readiness checks. The readiness probe selects this tag and nothing
        /// else, so a check that observes a condition every node shares must never carry it: gating rotation on
        /// such a condition drains the whole fleet at once.
        /// </summary>
        /// <remarks>
        /// The tag is namespaced rather than the conventional bare <c>ready</c> because the probe selects by tag
        /// across the whole shell container. A bare tag would silently enlist any other module's readiness check
        /// — including a shared-infrastructure check such as a Redis backplane probe — and reintroduce exactly
        /// the fleet-wide drain the split exists to prevent.
        /// </remarks>
        public const string ReadyTag = "contactcenter-ready";

        /// <summary>
        /// The tag applied to checks that consult an external dependency. These are alerting signals surfaced
        /// through the dependency probe and must never gate load balancer rotation.
        /// </summary>
        public const string DependencyTag = "contactcenter-dependency";

        /// <summary>
        /// The registration name of the node-local readiness check.
        /// </summary>
        public const string NodeCheckName = "contactcenter-node";

        /// <summary>
        /// The registration name of the opt-in node-local serving gate.
        /// </summary>
        public const string NodeServingCheckName = "contactcenter-node-serving";

        /// <summary>
        /// The registration name of the durable-storage reachability check.
        /// </summary>
        public const string StorageCheckName = "contactcenter-storage";

        /// <summary>
        /// The registration name of the event outbox backlog check.
        /// </summary>
        public const string OutboxCheckName = "contactcenter-outbox";

        /// <summary>
        /// The registration name of the provider ingress inbox backlog check.
        /// </summary>
        public const string ProviderIngressCheckName = "contactcenter-provider-ingress";

        /// <summary>
        /// The registration name of the deployment topology check.
        /// </summary>
        /// <remarks>
        /// This is the only readiness check that observes a condition every node shares. The exception is
        /// deliberate: a topology violation cannot self-heal, and serving traffic from a deployment that does
        /// not satisfy its declared support contract is the failure being prevented rather than collateral
        /// damage from preventing it.
        /// </remarks>
        public const string TopologyCheckName = "contactcenter-topology";

        /// <summary>
        /// The default path of the process liveness probe. It reports only that the process can serve a
        /// request and never consults a dependency, so a failing database or a growing backlog cannot trigger a
        /// restart.
        /// </summary>
        /// <remarks>
        /// Liveness is answered by host middleware placed ahead of the Orchard Core pipeline, not by a tenant
        /// feature. A route mapped inside a shell answers 404 whenever that tenant is disabled, renamed, or
        /// fails to start, and an orchestrator reads 404 as a probe failure — so a tenant-scoped liveness route
        /// restarts an otherwise healthy process for a tenant-level problem.
        /// <para>
        /// The path deliberately avoids <c>/health/live</c>, which is the default route of the
        /// <c>OrchardCore.HealthChecks</c> module. Host middleware short-circuits before routing, so taking that
        /// path would silently shadow that module's endpoint for every tenant in the process — including
        /// tenants that never enable Contact Center — and answer a permanent <c>200 Healthy</c> in its place.
        /// Shadowing a health endpoint with an unconditional success is a worse failure than any it could
        /// report.
        /// </para>
        /// </remarks>
        public const string ProcessLivenessPath = "/health/process";

        /// <summary>
        /// The route of the readiness probe. It aggregates every check tagged <see cref="ReadyTag"/>, which is
        /// only node-local state, and reports whether this node should receive traffic for this tenant.
        /// </summary>
        public const string ReadinessRoute = "api/contact-center/health/ready";

        /// <summary>
        /// The route of the dependency probe. It aggregates every check tagged <see cref="DependencyTag"/> and
        /// reports per-check detail, so it requires authorization and must never be wired to an orchestrator
        /// probe or a load balancer.
        /// </summary>
        public const string DependenciesRoute = "api/contact-center/health/dependencies";
    }

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
        /// The identifier of the Contact Center administration feature, which enables every capability's
        /// administration screens together.
        /// </summary>
        public const string Admin = "CrestApps.OrchardCore.ContactCenter.Admin";

        /// <summary>
        /// The identifier of the agent administration screens.
        /// </summary>
        public const string AgentsAdmin = "CrestApps.OrchardCore.ContactCenter.Agents.Admin";

        /// <summary>
        /// The identifier of the queue, skill, and business-hours administration screens.
        /// </summary>
        public const string QueuesAdmin = "CrestApps.OrchardCore.ContactCenter.Queues.Admin";

        /// <summary>
        /// The identifier of the outbound dialer administration screens.
        /// </summary>
        public const string DialerAdmin = "CrestApps.OrchardCore.ContactCenter.Dialer.Admin";

        /// <summary>
        /// The identifier of the recording and monitoring settings screens.
        /// </summary>
        public const string RecordingAdmin = "CrestApps.OrchardCore.ContactCenter.Recording.Admin";

        /// <summary>
        /// The identifier of the inbound entry-point administration screens.
        /// </summary>
        public const string EntryPointsAdmin = "CrestApps.OrchardCore.ContactCenter.EntryPoints.Admin";

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

        /// <summary>
        /// The identifier of the preview maintenance feature that exports, quiesces, resets, and verifies the
        /// Contact Center data of a preview tenant.
        /// </summary>
        public const string Maintenance = "CrestApps.OrchardCore.ContactCenter.Maintenance";
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
    /// Contains stable provider-result metadata keys describing a captured recording, shared between voice providers
    /// and the recording service that persists them onto the interaction.
    /// </summary>
    public static class RecordingMetadata
    {
        /// <summary>
        /// Identifies the recording as the provider itself names it, used as the retrieval reference when the
        /// provider reports no durable storage reference.
        /// </summary>
        public const string ProviderRecordingId = "providerRecordingId";

        /// <summary>
        /// Identifies the durable storage reference used to retrieve the recording media.
        /// </summary>
        public const string StorageReference = "storageReference";

        /// <summary>
        /// Identifies the recording media format.
        /// </summary>
        public const string Format = "format";

        /// <summary>
        /// Identifies the recording duration, in seconds, when the provider reports it.
        /// </summary>
        public const string DurationSeconds = "durationSeconds";

        /// <summary>
        /// Identifies the provider-relative path used to retrieve the stored recording.
        /// </summary>
        public const string RetrievalPath = "retrievalPath";
    }

    /// <summary>
    /// Contains stable machine-readable reason codes describing why a recording governance policy denied recording,
    /// shared between the governance policy and the recording service that records them on denial events.
    /// </summary>
    public static class RecordingGovernanceDenyReason
    {
        /// <summary>
        /// Recording is disabled for the tenant by the recording governance policy.
        /// </summary>
        public const string RecordingDisabled = "recordingDisabled";

        /// <summary>
        /// Recording requires explicit party consent that has not been captured on the interaction.
        /// </summary>
        public const string ConsentRequired = "consentRequired";
    }

    /// <summary>
    /// Contains stable machine-readable reason codes describing why a recording governance policy denied a
    /// right-to-erasure request, shared between the erasure service and the denial events it publishes.
    /// </summary>
    public static class RecordingErasureDenyReason
    {
        /// <summary>
        /// The interaction has no captured recording reference to erase.
        /// </summary>
        public const string NoRecording = "noRecording";

        /// <summary>
        /// The recording is under legal hold and is exempt from erasure until the hold is released.
        /// </summary>
        public const string LegalHold = "legalHold";
    }

    /// <summary>
    /// Contains stable request-metadata keys the transfer service passes to a voice provider so the provider can
    /// execute a resolved transfer destination without receiving raw client input.
    /// </summary>
    public static class TransferMetadata
    {
        /// <summary>
        /// Identifies the Orchard user id of the destination agent for an agent transfer, so a provider can resolve
        /// that agent's live endpoint. The client never supplies this; the transfer service resolves it server-side.
        /// </summary>
        public const string AgentUserId = "transferAgentUserId";
    }

    /// <summary>
    /// Contains stable request-metadata keys the conference service passes to a voice provider so the provider can
    /// add a resolved conference participant without receiving raw client input.
    /// </summary>
    public static class ConferenceMetadata
    {
        /// <summary>
        /// Identifies the Orchard user id of the agent to add to a live conversation as a conference participant, so
        /// a provider can resolve that agent's live endpoint. The client never supplies this; the conference service
        /// resolves it server-side.
        /// </summary>
        public const string AgentUserId = "conferenceAgentUserId";
    }

    /// <summary>
    /// Contains stable request- and result-metadata keys used to drive an attended (consultative) transfer across
    /// its begin, complete, and cancel phases so a provider can execute a resolved consult without receiving raw
    /// client input.
    /// </summary>
    public static class AttendedTransferMetadata
    {
        /// <summary>
        /// Identifies the Orchard user id of the destination agent to consult with, so a provider can resolve that
        /// agent's live endpoint. The client never supplies this; the transfer service resolves it server-side.
        /// </summary>
        public const string AgentUserId = "attendedTransferAgentUserId";
    }

    /// <summary>
    /// Contains site-settings configuration identifiers used by the Contact Center module set.
    /// </summary>
    public static class Settings
    {
        /// <summary>
        /// The site settings group identifier used for Contact Center administrative configuration.
        /// Every Contact Center settings display driver must use this group identifier so all
        /// Contact Center settings appear together on the same settings screen.
        /// </summary>
        public const string GroupId = "contactcenter";
    }

    /// <summary>
    /// Contains stable metadata keys written to call sessions and interactions for provider-reported telephony details.
    /// </summary>
    public static class TelephonyMetadata
    {
        /// <summary>
        /// The key under which the AMD (Answering Machine Detection) answer classification is stored.
        /// </summary>
        public const string AnswerClassification = "amd_answer_classification";
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
        /// Raised when an interaction transfer is denied by authorization or destination policy.
        /// </summary>
        public const string InteractionTransferDenied = "InteractionTransferDenied";

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
        /// Raised when a failed delivery attempt returns offered work to inbound routing.
        /// </summary>
        public const string OfferRequeued = "OfferRequeued";

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
        /// Raised when the recording governance policy denies a request to start or resume recording.
        /// </summary>
        public const string RecordingDenied = "RecordingDenied";

        /// <summary>
        /// Raised when a captured recording is accessed or retrieved, recording who accessed it and why for the
        /// recording access audit trail.
        /// </summary>
        public const string RecordingAccessed = "RecordingAccessed";

        /// <summary>
        /// Raised when a captured recording reference is erased at the orchestration layer in response to a
        /// right-to-erasure request, delegating media deletion to the owning media store.
        /// </summary>
        public const string RecordingErased = "RecordingErased";

        /// <summary>
        /// Raised when the recording governance policy denies a right-to-erasure request, for example because the
        /// recording is under legal hold.
        /// </summary>
        public const string RecordingErasureDenied = "RecordingErasureDenied";

        /// <summary>
        /// Raised when a supervisor starts monitoring, whispering, barging, or taking over a live call.
        /// </summary>
        public const string SupervisorMonitorStarted = "SupervisorMonitorStarted";

        /// <summary>
        /// Raised when a supervisor stops a monitoring, whisper, or barge engagement on a live call.
        /// </summary>
        public const string SupervisorMonitorStopped = "SupervisorMonitorStopped";
    }
}
