using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// One agent state change as a report reads it, whether it came from the state audit or from the older presence
/// events, so everything built on top reads one shape.
/// </summary>
internal sealed class AgentStateTransition
{
    /// <summary>
    /// Gets the agent profile identifier.
    /// </summary>
    public string AgentId { get; init; }

    /// <summary>
    /// Gets the identifier of the event the change was read from.
    /// </summary>
    public string EventId { get; init; }

    /// <summary>
    /// Gets the type of the event the change was read from.
    /// </summary>
    public string EventType { get; init; }

    /// <summary>
    /// Gets whether the change came from the state audit, which records every transition, rather than from the older
    /// presence events, which do not record reservations, calls or wrap-up.
    /// </summary>
    public bool FromAudit { get; init; }

    /// <summary>
    /// Gets when the change took effect.
    /// </summary>
    public DateTime ChangedUtc { get; init; }

    /// <summary>
    /// Gets when the change was written to the log, which is later than <see cref="ChangedUtc"/> for a sign-off dated
    /// by the agent's last heartbeat.
    /// </summary>
    public DateTime RecordedUtc { get; init; }

    /// <summary>
    /// Gets the state the change was recorded as leaving.
    /// </summary>
    public AgentPresenceStatus PreviousState { get; init; }

    /// <summary>
    /// Gets the state the change entered.
    /// </summary>
    public AgentPresenceStatus CurrentState { get; init; }

    /// <summary>
    /// Gets the state the agent asked for once their work ends, when one was pending.
    /// </summary>
    public AgentPresenceStatus? RequestedState { get; init; }

    /// <summary>
    /// Gets the reason code identifier, when the change carried one.
    /// </summary>
    public string ReasonCodeId { get; init; }

    /// <summary>
    /// Gets the reason as it read at the time.
    /// </summary>
    public string Reason { get; init; }

    /// <summary>
    /// Gets what caused the change, as one of the audit's sources, when the change came from the audit.
    /// </summary>
    public string Source { get; init; }

    /// <summary>
    /// Gets the interaction the change was about, for one routing made.
    /// </summary>
    public string InteractionId { get; init; }

    /// <summary>
    /// Gets the reservation the change was about, for one routing made.
    /// </summary>
    public string ReservationId { get; init; }

    /// <summary>
    /// Gets the queues the agent was signed in to after the change.
    /// </summary>
    public IReadOnlyList<string> QueueIds { get; init; } = [];

    /// <summary>
    /// Gets the campaigns the agent was signed in to after the change.
    /// </summary>
    public IReadOnlyList<string> CampaignIds { get; init; } = [];

    /// <summary>
    /// Gets the kind of actor that made the change.
    /// </summary>
    public ContactCenterActorType ActorType { get; init; }

    /// <summary>
    /// Gets the identifier of the actor that made the change.
    /// </summary>
    public string ActorId { get; init; }

    /// <summary>
    /// Gets whether the change signed the agent in: a sign-in event, or an audited change out of Offline.
    /// </summary>
    public bool IsSignIn
        => CurrentState != AgentPresenceStatus.Offline &&
            (string.Equals(EventType, ContactCenterConstants.Events.AgentSignedIn, StringComparison.Ordinal) ||
            (FromAudit && (PreviousState == AgentPresenceStatus.Offline || string.Equals(Source, AgentStateChangeSources.SignIn, StringComparison.Ordinal))));

    /// <summary>
    /// Gets whether the change signed the agent off, for whatever reason.
    /// </summary>
    public bool IsSignOff => CurrentState == AgentPresenceStatus.Offline;

    /// <summary>
    /// Gets or sets whether the change does not count, because it happened after a sign-off that was dated by the
    /// agent's last heartbeat and only recorded later: by then the agent was already gone.
    /// </summary>
    public bool Superseded { get; set; }

    /// <summary>
    /// Gets or sets the state the change should have been leaving, when the state it was recorded as leaving is not
    /// the one the record before it entered: a transition between the two is missing from the record.
    /// </summary>
    public AgentPresenceStatus? ExpectedPreviousState { get; set; }

    /// <summary>
    /// Gets whether a transition is missing from the record just before this one.
    /// </summary>
    public bool BreaksChain => ExpectedPreviousState.HasValue;
}
