using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Workflows.Services;

/// <summary>
/// Provides the canonical Contact Center domain event types as grouped, localized select options. The
/// entries are defined explicitly (rather than reflected from the constants) so each label is a
/// human-readable, extraction-friendly <c>S["..."]</c> string.
/// </summary>
public sealed class ContactCenterWorkflowEventTypeProvider : IContactCenterWorkflowEventTypeProvider
{
    private readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterWorkflowEventTypeProvider"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer used for the option labels.</param>
    public ContactCenterWorkflowEventTypeProvider(IStringLocalizer<ContactCenterWorkflowEventTypeProvider> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public IReadOnlyList<SelectListItem> GetEventTypes()
    {
        var interactions = new SelectListGroup { Name = S["Interaction"].Value };
        var activities = new SelectListGroup { Name = S["Activity"].Value };
        var routingAndQueues = new SelectListGroup { Name = S["Routing & queues"].Value };
        var agents = new SelectListGroup { Name = S["Agent"].Value };
        var offers = new SelectListGroup { Name = S["Offer"].Value };
        var dialer = new SelectListGroup { Name = S["Dialer"].Value };
        var callbacks = new SelectListGroup { Name = S["Callback"].Value };
        var calls = new SelectListGroup { Name = S["Call"].Value };
        var recording = new SelectListGroup { Name = S["Recording"].Value };
        var supervision = new SelectListGroup { Name = S["Supervision"].Value };
        var secureCapture = new SelectListGroup { Name = S["Secure capture"].Value };

        return
        [
            new SelectListItem(S["Any event type"].Value, string.Empty),

            new SelectListItem(S["Interaction created"].Value, ContactCenterConstants.Events.InteractionCreated) { Group = interactions },
            new SelectListItem(S["Interaction linked to activity"].Value, ContactCenterConstants.Events.InteractionLinkedToActivity) { Group = interactions },
            new SelectListItem(S["Interaction started"].Value, ContactCenterConstants.Events.InteractionStarted) { Group = interactions },
            new SelectListItem(S["Interaction updated"].Value, ContactCenterConstants.Events.InteractionUpdated) { Group = interactions },
            new SelectListItem(S["Interaction transferred"].Value, ContactCenterConstants.Events.InteractionTransferred) { Group = interactions },
            new SelectListItem(S["Interaction transfer denied"].Value, ContactCenterConstants.Events.InteractionTransferDenied) { Group = interactions },
            new SelectListItem(S["Interaction ended"].Value, ContactCenterConstants.Events.InteractionEnded) { Group = interactions },
            new SelectListItem(S["Interaction failed"].Value, ContactCenterConstants.Events.InteractionFailed) { Group = interactions },

            new SelectListItem(S["Activity reserved"].Value, ContactCenterConstants.Events.ActivityReserved) { Group = activities },
            new SelectListItem(S["Activity assignment changed"].Value, ContactCenterConstants.Events.ActivityAssignmentChanged) { Group = activities },
            new SelectListItem(S["Activity disposition applied"].Value, ContactCenterConstants.Events.ActivityDispositionApplied) { Group = activities },

            new SelectListItem(S["Routing decision made"].Value, ContactCenterConstants.Events.RoutingDecisionMade) { Group = routingAndQueues },
            new SelectListItem(S["Queue item added"].Value, ContactCenterConstants.Events.QueueItemAdded) { Group = routingAndQueues },
            new SelectListItem(S["Queue item reserved"].Value, ContactCenterConstants.Events.QueueItemReserved) { Group = routingAndQueues },
            new SelectListItem(S["Queue item assigned"].Value, ContactCenterConstants.Events.QueueItemAssigned) { Group = routingAndQueues },
            new SelectListItem(S["Queue item dequeued"].Value, ContactCenterConstants.Events.QueueItemDequeued) { Group = routingAndQueues },
            new SelectListItem(S["Queue item overflowed"].Value, ContactCenterConstants.Events.QueueItemOverflowed) { Group = routingAndQueues },
            new SelectListItem(S["Queue item withdrawn"].Value, ContactCenterConstants.Events.QueueItemWithdrawn) { Group = routingAndQueues },

            new SelectListItem(S["Agent signed in"].Value, ContactCenterConstants.Events.AgentSignedIn) { Group = agents },
            new SelectListItem(S["Agent signed out"].Value, ContactCenterConstants.Events.AgentSignedOut) { Group = agents },
            new SelectListItem(S["Agent presence changed"].Value, ContactCenterConstants.Events.AgentPresenceChanged) { Group = agents },
            new SelectListItem(S["Agent entitlements changed"].Value, ContactCenterConstants.Events.AgentEntitlementsChanged) { Group = agents },
            new SelectListItem(S["Agent reserved"].Value, ContactCenterConstants.Events.AgentReserved) { Group = agents },
            new SelectListItem(S["Agent released"].Value, ContactCenterConstants.Events.AgentReleased) { Group = agents },

            new SelectListItem(S["Offer accepted"].Value, ContactCenterConstants.Events.OfferAccepted) { Group = offers },
            new SelectListItem(S["Offer declined"].Value, ContactCenterConstants.Events.OfferDeclined) { Group = offers },
            new SelectListItem(S["Offer requeued"].Value, ContactCenterConstants.Events.OfferRequeued) { Group = offers },

            new SelectListItem(S["Dialer run started"].Value, ContactCenterConstants.Events.DialerRunStarted) { Group = dialer },
            new SelectListItem(S["Dialer attempt scheduled"].Value, ContactCenterConstants.Events.DialerAttemptScheduled) { Group = dialer },
            new SelectListItem(S["Dialer attempt started"].Value, ContactCenterConstants.Events.DialerAttemptStarted) { Group = dialer },
            new SelectListItem(S["Dialer attempt completed"].Value, ContactCenterConstants.Events.DialerAttemptCompleted) { Group = dialer },
            new SelectListItem(S["Dial suppressed"].Value, ContactCenterConstants.Events.DialSuppressed) { Group = dialer },
            new SelectListItem(S["Manual dial suppressed"].Value, ContactCenterConstants.Events.ManualDialSuppressed) { Group = dialer },

            new SelectListItem(S["Callback scheduled"].Value, ContactCenterConstants.Events.CallbackScheduled) { Group = callbacks },
            new SelectListItem(S["Callback promoted"].Value, ContactCenterConstants.Events.CallbackPromoted) { Group = callbacks },
            new SelectListItem(S["Callback completed"].Value, ContactCenterConstants.Events.CallbackCompleted) { Group = callbacks },

            new SelectListItem(S["Call session created"].Value, ContactCenterConstants.Events.CallSessionCreated) { Group = calls },
            new SelectListItem(S["Call session updated"].Value, ContactCenterConstants.Events.CallSessionUpdated) { Group = calls },
            new SelectListItem(S["Call connected"].Value, ContactCenterConstants.Events.CallConnected) { Group = calls },
            new SelectListItem(S["Call held"].Value, ContactCenterConstants.Events.CallHeld) { Group = calls },
            new SelectListItem(S["Call resumed"].Value, ContactCenterConstants.Events.CallResumed) { Group = calls },
            new SelectListItem(S["Call muted"].Value, ContactCenterConstants.Events.CallMuted) { Group = calls },
            new SelectListItem(S["Call unmuted"].Value, ContactCenterConstants.Events.CallUnmuted) { Group = calls },
            new SelectListItem(S["Call conference changed"].Value, ContactCenterConstants.Events.CallConferenceChanged) { Group = calls },
            new SelectListItem(S["Call ended"].Value, ContactCenterConstants.Events.CallEnded) { Group = calls },
            new SelectListItem(S["Call sent to voicemail"].Value, ContactCenterConstants.Events.CallSentToVoicemail) { Group = calls },
            new SelectListItem(S["Repeated poor call quality"].Value, ContactCenterConstants.Events.CallQualityAlertRaised) { Group = calls },

            new SelectListItem(S["Recording started"].Value, ContactCenterConstants.Events.RecordingStarted) { Group = recording },
            new SelectListItem(S["Recording paused"].Value, ContactCenterConstants.Events.RecordingPaused) { Group = recording },
            new SelectListItem(S["Recording resumed"].Value, ContactCenterConstants.Events.RecordingResumed) { Group = recording },
            new SelectListItem(S["Recording auto-resumed"].Value, ContactCenterConstants.Events.RecordingAutoResumed) { Group = recording },
            new SelectListItem(S["Recording stopped"].Value, ContactCenterConstants.Events.RecordingStopped) { Group = recording },
            new SelectListItem(S["Recording denied"].Value, ContactCenterConstants.Events.RecordingDenied) { Group = recording },
            new SelectListItem(S["Recording accessed"].Value, ContactCenterConstants.Events.RecordingAccessed) { Group = recording },
            new SelectListItem(S["Recording erased"].Value, ContactCenterConstants.Events.RecordingErased) { Group = recording },
            new SelectListItem(S["Recording erasure denied"].Value, ContactCenterConstants.Events.RecordingErasureDenied) { Group = recording },
            new SelectListItem(S["Recording media deleted"].Value, ContactCenterConstants.Events.RecordingMediaDeleted) { Group = recording },

            new SelectListItem(S["Supervisor monitor started"].Value, ContactCenterConstants.Events.SupervisorMonitorStarted) { Group = supervision },
            new SelectListItem(S["Supervisor monitor stopped"].Value, ContactCenterConstants.Events.SupervisorMonitorStopped) { Group = supervision },

            new SelectListItem(S["Secure capture started"].Value, ContactCenterConstants.Events.SecureCaptureStarted) { Group = secureCapture },
            new SelectListItem(S["Secure capture completed"].Value, ContactCenterConstants.Events.SecureCaptureCompleted) { Group = secureCapture },
            new SelectListItem(S["Secure capture cancelled"].Value, ContactCenterConstants.Events.SecureCaptureCancelled) { Group = secureCapture },

            new SelectListItem(S["Agent state changed"].Value, ContactCenterConstants.Events.AgentStateChanged) { Group = agents },
            new SelectListItem(S["Agent connected"].Value, ContactCenterConstants.Events.AgentConnected) { Group = agents },
            new SelectListItem(S["Agent disconnected"].Value, ContactCenterConstants.Events.AgentDisconnected) { Group = agents },
            new SelectListItem(S["Agent heartbeat lost"].Value, ContactCenterConstants.Events.AgentHeartbeatLost) { Group = agents },

            new SelectListItem(S["Offer presented"].Value, ContactCenterConstants.Events.OfferPresented) { Group = offers },
            new SelectListItem(S["Offer expired"].Value, ContactCenterConstants.Events.OfferExpired) { Group = offers },
            new SelectListItem(S["Offer missed"].Value, ContactCenterConstants.Events.OfferMissed) { Group = offers },
            new SelectListItem(S["Offer cancelled"].Value, ContactCenterConstants.Events.OfferCancelled) { Group = offers },

            new SelectListItem(S["Call queued"].Value, ContactCenterConstants.Events.CallQueued) { Group = calls },
            new SelectListItem(S["Call dequeued"].Value, ContactCenterConstants.Events.CallDequeued) { Group = calls },
            new SelectListItem(S["Dial started"].Value, ContactCenterConstants.Events.DialStarted) { Group = calls },
            new SelectListItem(S["Dial failed"].Value, ContactCenterConstants.Events.DialFailed) { Group = calls },
            new SelectListItem(S["Agent leg answered"].Value, ContactCenterConstants.Events.AgentLegAnswered) { Group = calls },
            new SelectListItem(S["Agent leg failed"].Value, ContactCenterConstants.Events.AgentLegFailed) { Group = calls },
            new SelectListItem(S["Call abandoned"].Value, ContactCenterConstants.Events.CallAbandoned) { Group = calls },
            new SelectListItem(S["Consult started"].Value, ContactCenterConstants.Events.ConsultStarted) { Group = calls },
            new SelectListItem(S["Consult connected"].Value, ContactCenterConstants.Events.ConsultConnected) { Group = calls },
            new SelectListItem(S["Consult completed"].Value, ContactCenterConstants.Events.ConsultCompleted) { Group = calls },
            new SelectListItem(S["Consult cancelled"].Value, ContactCenterConstants.Events.ConsultCancelled) { Group = calls },
            new SelectListItem(S["AI call answered"].Value, ContactCenterConstants.Events.AiCallAnswered) { Group = calls },
            new SelectListItem(S["AI answerer detected"].Value, ContactCenterConstants.Events.AiAnswererDetected) { Group = calls },
            new SelectListItem(S["AI conversation ended"].Value, ContactCenterConstants.Events.AiConversationEnded) { Group = calls },
            new SelectListItem(S["AI handoff requested"].Value, ContactCenterConstants.Events.AiHandoffRequested) { Group = calls },
            new SelectListItem(S["Extension call started"].Value, ContactCenterConstants.Events.ExtensionCallStarted) { Group = calls },
            new SelectListItem(S["Extension call ended"].Value, ContactCenterConstants.Events.ExtensionCallEnded) { Group = calls },
        ];
    }
}
