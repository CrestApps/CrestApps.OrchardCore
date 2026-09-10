namespace CrestApps.OrchardCore.ContactCenter;

public static partial class ContactCenterConstants
{
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
        /// Raised when the outbound compliance gate suppresses a manual, agent-initiated soft-phone call.
        /// </summary>
        public const string ManualDialSuppressed = "ManualDialSuppressed";

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
        /// Raised when a ringing call is sent to an agent's voicemail. The platform answers the provider leg to
        /// record the caller's message; this event lets the soft-phone projection mark the call as a missed call
        /// for the target agent before that answer arrives, so the recording leg never surfaces as a live call.
        /// </summary>
        public const string CallSentToVoicemail = "CallSentToVoicemail";

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
        /// Raised when a paused recording is automatically resumed by the platform after the tenant's maximum
        /// secure-pause window elapses, so a sensitive-data pause can never silently suppress capture forever.
        /// </summary>
        public const string RecordingAutoResumed = "RecordingAutoResumed";

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
        /// Raised when the underlying recording media has actually been deleted from the owning media store, as
        /// the confirmed-deletion receipt that follows a <see cref="RecordingErased"/> request. This is the
        /// durable proof of completed deletion, distinct from mere acceptance of the erasure request.
        /// </summary>
        public const string RecordingMediaDeleted = "RecordingMediaDeleted";

        /// <summary>
        /// Raised when a supervisor starts monitoring, whispering, barging, or taking over a live call.
        /// </summary>
        public const string SupervisorMonitorStarted = "SupervisorMonitorStarted";

        /// <summary>
        /// Raised when a supervisor stops a monitoring, whisper, or barge engagement on a live call.
        /// </summary>
        public const string SupervisorMonitorStopped = "SupervisorMonitorStopped";

        /// <summary>
        /// Raised when an agent starts an agent-assisted secure capture, sending the customer to a secure page to
        /// enter sensitive data (such as a payment card or a national identifier) so it is masked from the agent
        /// and never enters the recording.
        /// </summary>
        public const string SecureCaptureStarted = "SecureCaptureStarted";

        /// <summary>
        /// Raised when a customer completes a secure capture. The event carries only the masked representation and
        /// the tokenization reference; the raw sensitive value is never part of the event.
        /// </summary>
        public const string SecureCaptureCompleted = "SecureCaptureCompleted";

        /// <summary>
        /// Raised when a secure capture is cancelled by the agent or expires before the customer completes it.
        /// </summary>
        public const string SecureCaptureCancelled = "SecureCaptureCancelled";
    }
}
