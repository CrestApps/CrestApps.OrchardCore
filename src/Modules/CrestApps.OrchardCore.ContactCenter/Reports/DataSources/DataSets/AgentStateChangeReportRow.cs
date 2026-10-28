using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Reports.DataSources.DataSets;

/// <summary>
/// One row of the agent state changes data set: an agent state change read from the event log, with the values of its
/// payload.
/// </summary>
public sealed class AgentStateChangeReportRow
{
    /// <summary>
    /// Gets the identifier of the event that recorded the change.
    /// </summary>
    public string EventId { get; init; }

    /// <summary>
    /// Gets the type of the event that recorded the change.
    /// </summary>
    public string EventType { get; init; }

    /// <summary>
    /// Gets a value indicating whether the change comes from the state audit, which records every transition, rather
    /// than from the older presence events.
    /// </summary>
    public bool FromAudit { get; init; }

    /// <summary>
    /// Gets the agent profile identifier.
    /// </summary>
    public string AgentId { get; init; }

    /// <summary>
    /// Gets the user the agent signs in as.
    /// </summary>
    public string UserId { get; init; }

    /// <summary>
    /// Gets the state before the change.
    /// </summary>
    public AgentPresenceStatus? PreviousState { get; init; }

    /// <summary>
    /// Gets the state after the change. A sign-out is always a change to <see cref="AgentPresenceStatus.Offline"/>.
    /// </summary>
    public AgentPresenceStatus? CurrentState { get; init; }

    /// <summary>
    /// Gets the state the agent asked to move to once their work ends, when one was pending.
    /// </summary>
    public AgentPresenceStatus? RequestedState { get; init; }

    /// <summary>
    /// Gets the identifier of the reason code chosen for the new state.
    /// </summary>
    public string ReasonCodeId { get; init; }

    /// <summary>
    /// Gets the reason as it read at the time.
    /// </summary>
    public string Reason { get; init; }

    /// <summary>
    /// Gets what caused the change.
    /// </summary>
    public string Source { get; init; }

    /// <summary>
    /// Gets the interaction the change is about, for a change routing made.
    /// </summary>
    public string InteractionId { get; init; }

    /// <summary>
    /// Gets the reservation the change is about, for a change routing made.
    /// </summary>
    public string ReservationId { get; init; }

    /// <summary>
    /// Gets why a reservation ended without the work being taken.
    /// </summary>
    public string ReleaseReason { get; init; }

    /// <summary>
    /// Gets the agent session the change belongs to.
    /// </summary>
    public string AgentSessionId { get; init; }

    /// <summary>
    /// Gets the queues the agent is signed in to after the change.
    /// </summary>
    public IReadOnlyList<string> QueueIds { get; init; } = [];

    /// <summary>
    /// Gets the campaigns the agent is signed in to after the change.
    /// </summary>
    public IReadOnlyList<string> CampaignIds { get; init; } = [];

    /// <summary>
    /// Gets when the change took effect, or <see langword="null"/> when the payload could not be read.
    /// </summary>
    public DateTime? ChangedUtc { get; init; }

    /// <summary>
    /// Gets when the event occurred.
    /// </summary>
    public DateTime OccurredUtc { get; init; }

    /// <summary>
    /// Gets when the event was recorded.
    /// </summary>
    public DateTime RecordedUtc { get; init; }

    /// <summary>
    /// Gets the kind of actor that made the change.
    /// </summary>
    public ContactCenterActorType ActorType { get; init; }

    /// <summary>
    /// Gets the identifier of the actor that made the change.
    /// </summary>
    public string ActorId { get; init; }

    /// <summary>
    /// Creates the row of an agent state event. The payload is read as the state audit or as the older presence
    /// events wrote it; a payload that cannot be read leaves the payload's values empty.
    /// </summary>
    /// <param name="interactionEvent">The event, already brought to the current schema version.</param>
    /// <param name="payloadReadable">Whether the event's payload may be read.</param>
    /// <returns>The row.</returns>
    public static AgentStateChangeReportRow Create(InteractionEvent interactionEvent, bool payloadReadable)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        var fromAudit = string.Equals(interactionEvent.EventType, ContactCenterConstants.Events.AgentStateChanged, StringComparison.Ordinal);

        if (payloadReadable && fromAudit && TryRead<AgentStateChangedEventData>(interactionEvent) is { } change)
        {
            return new AgentStateChangeReportRow
            {
                EventId = interactionEvent.ItemId,
                EventType = interactionEvent.EventType,
                FromAudit = true,
                AgentId = interactionEvent.AggregateId,
                UserId = change.UserId,
                PreviousState = change.PreviousState,
                CurrentState = change.CurrentState,
                RequestedState = change.RequestedState,
                ReasonCodeId = change.ReasonCodeId,
                Reason = change.ReasonName,
                Source = change.Source,
                InteractionId = change.InteractionId,
                ReservationId = change.ReservationId,
                ReleaseReason = change.ReleaseReason,
                AgentSessionId = change.AgentSessionId,
                QueueIds = [.. change.QueueIds ?? []],
                CampaignIds = [.. change.CampaignIds ?? []],
                ChangedUtc = change.ChangedUtc == default ? interactionEvent.OccurredUtc : change.ChangedUtc,
                OccurredUtc = interactionEvent.OccurredUtc,
                RecordedUtc = interactionEvent.RecordedUtc,
                ActorType = interactionEvent.ActorType,
                ActorId = interactionEvent.ActorId,
            };
        }

        if (payloadReadable && !fromAudit && TryRead<AgentPresenceChangedEventData>(interactionEvent) is { } presence)
        {
            var signedOut = string.Equals(interactionEvent.EventType, ContactCenterConstants.Events.AgentSignedOut, StringComparison.Ordinal);

            return new AgentStateChangeReportRow
            {
                EventId = interactionEvent.ItemId,
                EventType = interactionEvent.EventType,
                FromAudit = false,
                AgentId = interactionEvent.AggregateId,
                UserId = presence.UserId,
                PreviousState = presence.PreviousStatus,
                CurrentState = signedOut
                    ? AgentPresenceStatus.Offline
                    : presence.CurrentStatus,
                RequestedState = presence.RequestedStatus,
                Reason = presence.Reason,
                QueueIds = [.. presence.QueueIds ?? []],
                CampaignIds = [.. presence.CampaignIds ?? []],
                ChangedUtc = presence.ChangedUtc == default ? interactionEvent.OccurredUtc : presence.ChangedUtc,
                OccurredUtc = interactionEvent.OccurredUtc,
                RecordedUtc = interactionEvent.RecordedUtc,
                ActorType = interactionEvent.ActorType,
                ActorId = interactionEvent.ActorId,
            };
        }

        return new AgentStateChangeReportRow
        {
            EventId = interactionEvent.ItemId,
            EventType = interactionEvent.EventType,
            FromAudit = fromAudit,
            AgentId = interactionEvent.AggregateId,
            OccurredUtc = interactionEvent.OccurredUtc,
            RecordedUtc = interactionEvent.RecordedUtc,
            ActorType = interactionEvent.ActorType,
            ActorId = interactionEvent.ActorId,
        };
    }

    private static T TryRead<T>(InteractionEvent interactionEvent)
        where T : class
    {
        try
        {
            return interactionEvent.GetData<T>();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
