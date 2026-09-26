using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Decides which domain events one provider report raises on a call session: the ones its change of state, mute,
/// recording and participation calls for, less a call end that is already on record.
/// </summary>
/// <remarks>
/// A call ends once. The reject command records a rejected call's end itself, because the provider may never report
/// a hangup for a call it was told to refuse; when the provider does report one, the session sees its own first
/// terminal transition and would record the same end a second time, counting the call twice in every report that
/// counts ends and re-running everything an end triggers.
/// </remarks>
internal static class CallSessionEventTypes
{
    /// <summary>
    /// Resolves the events for a session that moved from <paramref name="previous"/> to its current state.
    /// </summary>
    /// <param name="previous">The session as it was before the report was applied.</param>
    /// <param name="current">The session with the report applied.</param>
    /// <param name="interactionWasSettled">Whether the interaction had already ended before this report.</param>
    /// <param name="interactionId">The interaction, whose history is read only for a repeated end.</param>
    /// <param name="eventStore">The interaction event store.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public static async Task<List<string>> ResolveAsync(
        CallSessionSnapshot previous,
        CallSession current,
        bool interactionWasSettled,
        string interactionId,
        IInteractionEventStore eventStore,
        CancellationToken cancellationToken)
    {
        var eventTypes = Resolve(previous, CallSessionSnapshot.Of(current));

        // Only a call already settled can be hearing about its own end a second time; a live call, or one handed back
        // to routing, has not ended yet whatever an earlier attempt recorded.
        if (interactionWasSettled &&
            eventTypes.Contains(ContactCenterConstants.Events.CallEnded) &&
            await IsEndOnRecordAsync(interactionId, eventStore, cancellationToken))
        {
            eventTypes.Remove(ContactCenterConstants.Events.CallEnded);
        }

        return eventTypes;
    }

    internal static List<string> Resolve(CallSessionSnapshot previous, CallSessionSnapshot current)
    {
        var eventTypes = new List<string>
        {
            ContactCenterConstants.Events.CallSessionUpdated,
        };

        if (current.State == VoiceCallState.Connected && previous.State != VoiceCallState.Connected)
        {
            eventTypes.Add(ContactCenterConstants.Events.CallConnected);
        }

        if (current.State == VoiceCallState.OnHold && previous.State != VoiceCallState.OnHold)
        {
            eventTypes.Add(ContactCenterConstants.Events.CallHeld);
        }

        if (previous.State == VoiceCallState.OnHold && current.State == VoiceCallState.Connected)
        {
            eventTypes.Add(ContactCenterConstants.Events.CallResumed);
        }

        if (current.IsMuted && !previous.IsMuted)
        {
            eventTypes.Add(ContactCenterConstants.Events.CallMuted);
        }

        if (!current.IsMuted && previous.IsMuted)
        {
            eventTypes.Add(ContactCenterConstants.Events.CallUnmuted);
        }

        if (current.RecordingState != previous.RecordingState)
        {
            eventTypes.AddRange(ResolveRecordingEvents(previous.RecordingState, current.RecordingState));
        }

        // Participation now changes as legs join and leave the bridge, not only when a provider publishes a
        // conference count. An ordinary two-party call gaining its customer and agent legs is not a conference
        // change, so the event stays scoped to calls that are, or have just stopped being, a conference.
        if (current.IsConference != previous.IsConference ||
            ((current.IsConference || previous.IsConference) && current.ParticipantCount != previous.ParticipantCount))
        {
            eventTypes.Add(ContactCenterConstants.Events.CallConferenceChanged);
        }

        if (CallSessionLifecycle.IsTerminal(current.State) && !CallSessionLifecycle.IsTerminal(previous.State))
        {
            eventTypes.Add(ContactCenterConstants.Events.CallEnded);
        }

        return eventTypes;
    }

    private static async Task<bool> IsEndOnRecordAsync(string interactionId, IInteractionEventStore eventStore, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(interactionId))
        {
            return false;
        }

        var history = await eventStore.GetByInteractionAsync(interactionId, cancellationToken);

        return history?.Any(interactionEvent =>
            string.Equals(interactionEvent.EventType, ContactCenterConstants.Events.CallEnded, StringComparison.Ordinal)) == true;
    }

    private static string[] ResolveRecordingEvents(RecordingState previousState, RecordingState currentState)
    {
        if (currentState == previousState)
        {
            return [];
        }

        return currentState switch
        {
            RecordingState.Recording when previousState == RecordingState.Paused
                => [ContactCenterConstants.Events.RecordingResumed],
            RecordingState.Recording => [ContactCenterConstants.Events.RecordingStarted],
            RecordingState.Paused => [ContactCenterConstants.Events.RecordingPaused],
            RecordingState.Stopped => [ContactCenterConstants.Events.RecordingStopped],
            _ => [],
        };
    }
}

/// <summary>
/// The parts of a call session that decide which events a change to it raises.
/// </summary>
/// <param name="State">The call state.</param>
/// <param name="IsMuted">Whether the call is muted.</param>
/// <param name="RecordingState">The recording state.</param>
/// <param name="IsConference">Whether the call is a conference.</param>
/// <param name="ParticipantCount">How many parties the call has.</param>
internal readonly record struct CallSessionSnapshot(
    VoiceCallState State,
    bool IsMuted,
    RecordingState RecordingState,
    bool IsConference,
    int ParticipantCount)
{
    /// <summary>
    /// Takes the snapshot of a session as it is now.
    /// </summary>
    /// <param name="session">The session.</param>
    public static CallSessionSnapshot Of(CallSession session)
        => new(session.State, session.IsMuted, session.RecordingState, session.IsConference, session.ParticipantCount);
}
