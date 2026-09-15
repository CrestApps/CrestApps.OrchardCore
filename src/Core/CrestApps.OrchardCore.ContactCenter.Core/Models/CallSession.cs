using System.ComponentModel;
using System.Text.Json.Serialization;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the Contact Center's business-oriented projection of a voice call. It maps a provider
/// call to an interaction, agent, and queue, and tracks the normalized call lifecycle and durations
/// without owning media execution, which remains the responsibility of the Telephony provider.
/// </summary>
public sealed class CallSession : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the identifier of the interaction this call session belongs to.
    /// </summary>
    public string InteractionId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the CRM activity the call belongs to.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the provider that owns the call.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider-specific identifier of the call.
    /// </summary>
    public string ProviderCallId { get; set; }

    /// <summary>
    /// Gets or sets the delivery model used to deliver the call to the agent.
    /// </summary>
    public VoiceProviderDeliveryModel DeliveryModel { get; set; }

    /// <summary>
    /// Gets or sets the direction of the call relative to the contact center.
    /// </summary>
    public InteractionDirection Direction { get; set; }

    /// <summary>
    /// Gets or sets the normalized call state.
    /// </summary>
    [JsonInclude]
    public VoiceCallState State { get; private set; }

    /// <summary>
    /// Moves the call session to the specified state.
    /// </summary>
    /// <param name="state">The state to move to.</param>
    /// <exception cref="InvalidStateTransitionException">The session cannot reach the state from the one it is in.</exception>
    public void TransitionTo(VoiceCallState state)
    {
        if (!CallSessionLifecycle.CanTransition(State, state))
        {
            throw new InvalidStateTransitionException(nameof(CallSession), State, state);
        }

        State = state;
    }

    /// <summary>
    /// Determines whether the call session can move to the specified state.
    /// </summary>
    /// <param name="state">The state to test.</param>
    /// <returns><see langword="true"/> when the transition is admitted; otherwise <see langword="false"/>.</returns>
    public bool CanTransitionTo(VoiceCallState state)
        => CallSessionLifecycle.CanTransition(State, state);

    /// <summary>
    /// Gets a value indicating whether the call has reached an outcome it cannot leave.
    /// </summary>
    [JsonIgnore]
    public bool IsTerminal => CallSessionLifecycle.IsTerminal(State);

    /// <summary>
    /// Records the state the provider reports the call is in.
    /// </summary>
    /// <param name="state">The state observed on the provider.</param>
    /// <remarks>
    /// The provider owns what the call is actually doing, so this is a report rather than a decision. Ordering
    /// for that stream is enforced upstream by <c>VoiceStreamOrdering</c>, which refuses deliveries that would
    /// move the call backwards; applying a second, narrower rule here would let this record disagree with the
    /// interaction it is projected onto, which is the divergence <c>CallStateMachinePropertyTests</c> catches.
    /// </remarks>
    public void MirrorProviderState(VoiceCallState state)
    {
        // Mirroring reports what the provider already did, so it does not consult the table. It still refuses to
        // bring a terminal session back to life: a call that this system already recorded as over cannot start
        // alerting again, and a late provider frame that reopened it would merge two calls into one history.
        if (CallSessionLifecycle.IsTerminal(State) && !CallSessionLifecycle.IsTerminal(state))
        {
            return;
        }

        State = state;
    }

    /// <summary>
    /// Restores a state that was decided elsewhere, without consulting the lifecycle.
    /// </summary>
    /// <param name="state">The state to restore.</param>
    /// <returns>The same session, so it can be used at the end of an object initializer.</returns>
    /// <remarks>
    /// This bypasses every transition rule and exists only so a test can arrange a state directly. Production code
    /// must never call it: <c>AggregateLifecycleArchitectureTests</c> fails the build if any file under <c>src/</c>
    /// does, so the bypass cannot quietly become a shortcut.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public CallSession RestorePersistedState(VoiceCallState state)
    {
        State = state;

        return this;
    }

    /// <summary>
    /// Gets or sets the identifier of the agent connected to the call.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the active agent-session identifier that owns the live media leg for this call.
    /// </summary>
    public string AgentSessionId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the queue the call was delivered from.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the legs that make up the call. A leg is one party's connection; <see cref="Bridge"/>
    /// describes which of them currently hear one another.
    /// </summary>
    public IList<CallLeg> Legs { get; set; } = [];

    /// <summary>
    /// Gets or sets the media topology that joins the legs, including its full membership history.
    /// </summary>
    public Bridge Bridge { get; set; }

    /// <summary>
    /// Gets or sets the bridges the call previously occupied, retained so membership at a past instant stays
    /// reconstructible after the parties were moved to a different media topology.
    /// </summary>
    public IList<Bridge> PriorBridges { get; set; } = [];

    /// <summary>
    /// Gets or sets the private consults placed from this call.
    /// </summary>
    public IList<ConsultCall> Consults { get; set; } = [];

    /// <summary>
    /// Gets or sets the links from this call to the calls it was transferred from, transferred to, consulted,
    /// or conferenced with.
    /// </summary>
    public IList<CallRelationship> Relationships { get; set; } = [];

    /// <summary>
    /// Gets or sets the supervisor engagements recorded against this call, live and past.
    /// </summary>
    public IList<MonitorSession> MonitorSessions { get; set; } = [];

    /// <summary>
    /// Gets or sets the provider recording identifier for the active or retained call recording.
    /// </summary>
    public string RecordingId { get; set; }

    /// <summary>
    /// Gets or sets the durable provider-command identifier that last fenced a topology transition.
    /// </summary>
    public string DurableCommandId { get; set; }

    /// <summary>
    /// Gets or sets the address of the calling party.
    /// </summary>
    public string FromAddress { get; set; }

    /// <summary>
    /// Gets or sets the address of the called party.
    /// </summary>
    public string ToAddress { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the call is currently on hold.
    /// </summary>
    public bool IsOnHold { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider reports the call as muted.
    /// </summary>
    public bool IsMuted { get; set; }

    /// <summary>
    /// Gets or sets the provider-reported recording state for the call.
    /// </summary>
    public RecordingState RecordingState { get; set; }

    /// <summary>
    /// Gets or sets the provider recording reference for the call, when one exists.
    /// </summary>
    public string RecordingReference { get; set; }

    /// <summary>
    /// Gets or sets the UTC time of the latest provider event applied to this call session.
    /// </summary>
    public DateTime? LastProviderEventUtc { get; set; }

    /// <summary>
    /// Gets or sets the highest provider-supplied monotonic sequence number applied to this call
    /// session. When a provider supplies monotonic sequence numbers this value is the authoritative
    /// ordering high-water mark; deliveries at or below it are rejected as stale. It remains
    /// <see langword="null"/> for providers that only supply timestamps or idempotency keys.
    /// </summary>
    public long? HighWaterSequence { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the call session was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the call started dialing or ringing.
    /// </summary>
    public DateTime? StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the call was answered or connected.
    /// </summary>
    public DateTime? AnsweredUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the call ended.
    /// </summary>
    public DateTime? EndedUtc { get; set; }

    /// <summary>
    /// Gets or sets the provider-neutral reason the call ended. It is assigned whenever the session
    /// reaches a terminal state so outbound compliance reporting and abandon analytics can distinguish
    /// a normal clearing from a busy, unanswered, rejected, congested, abandoned, or machine-answered
    /// call at the source rather than inferring it later.
    /// </summary>
    public HangupCause? HangupCause { get; set; }

    /// <summary>
    /// Gets or sets the total seconds the call was connected (talk time).
    /// </summary>
    public double TalkSeconds { get; set; }

    /// <summary>
    /// Gets or sets the total seconds the call spent on hold.
    /// </summary>
    public double HoldSeconds { get; set; }

    /// <summary>
    /// Gets or sets provider-specific metadata retained for troubleshooting.
    /// </summary>
    public IDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets the UTC time the call session was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }

    /// <summary>
    /// Gets a value indicating whether three or more parties currently hear one another.
    /// </summary>
    [JsonIgnore]
    public bool IsConference => Bridge?.Kind == BridgeKind.Conference;

    /// <summary>
    /// Gets the number of parties currently present on the media topology. The provider's own live count
    /// wins when it publishes one, because a provider sees parties the platform never created a leg for.
    /// </summary>
    [JsonIgnore]
    public int ParticipantCount
        => Bridge?.ReportedParticipantCount ?? Bridge?.ActiveParticipants.Count() ?? 0;

    /// <summary>
    /// Gets the supervisor engagement that is currently live, when there is one.
    /// </summary>
    [JsonIgnore]
    public IEnumerable<MonitorSession> ActiveMonitorSessions
        => MonitorSessions.Where(monitorSession => monitorSession.IsActive);

    /// <summary>
    /// Gets the parties that were joined to any of the call's bridges at the given instant, including bridges
    /// the call has since been moved off.
    /// </summary>
    /// <param name="instant">The UTC instant to reconstruct membership for.</param>
    /// <returns>The participants that were joined at that instant.</returns>
    public IEnumerable<BridgeParticipant> ParticipantsAt(DateTime instant)
    {
        foreach (var priorBridge in PriorBridges)
        {
            foreach (var participant in priorBridge.ParticipantsAt(instant))
            {
                yield return participant;
            }
        }

        if (Bridge is null)
        {
            yield break;
        }

        foreach (var participant in Bridge.ParticipantsAt(instant))
        {
            yield return participant;
        }
    }
}
