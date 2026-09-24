using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

/// <summary>
/// Builds the events the audit writers record, shaped exactly as the shared contract says, so the reports are tested
/// against the contract rather than against whatever a writer happens to do today.
/// </summary>
internal static class AuditEvents
{
    private static int _sequence;

    public static InteractionEvent State(
        string agentId,
        DateTime changedUtc,
        AgentPresenceStatus previous,
        AgentPresenceStatus current,
        string source = AgentStateChangeSources.SetState,
        DateTime? recordedUtc = null,
        ContactCenterActorType actorType = ContactCenterActorType.Agent,
        string interactionId = null,
        string reason = null)
    {
        var interactionEvent = new InteractionEvent
        {
            ItemId = NextId(),
            EventType = ContactCenterConstants.Events.AgentStateChanged,
            AggregateType = nameof(AgentProfile),
            AggregateId = agentId,
            InteractionId = interactionId,
            OccurredUtc = changedUtc,
            RecordedUtc = recordedUtc ?? changedUtc,
            ActorType = actorType,
            ActorId = actorType == ContactCenterActorType.Agent ? "user-" + agentId : ContactCenterConstants.SystemActor,
        };

        interactionEvent.SetData(new AgentStateChangedEventData
        {
            AgentId = agentId,
            PreviousState = previous,
            CurrentState = current,
            Source = source,
            InteractionId = interactionId,
            ReasonName = reason,
            ChangedUtc = changedUtc,
        });

        return interactionEvent;
    }

    public static InteractionEvent Presence(
        string agentId,
        DateTime changedUtc,
        AgentPresenceStatus previous,
        AgentPresenceStatus current,
        string eventType = ContactCenterConstants.Events.AgentPresenceChanged)
    {
        var interactionEvent = new InteractionEvent
        {
            ItemId = NextId(),
            EventType = eventType,
            AggregateType = nameof(AgentProfile),
            AggregateId = agentId,
            OccurredUtc = changedUtc,
        };

        interactionEvent.SetData(new AgentPresenceChangedEventData
        {
            PreviousStatus = previous,
            CurrentStatus = current,
            ChangedUtc = changedUtc,
        });

        return interactionEvent;
    }

    public static InteractionEvent Call(
        string eventType,
        string interactionId,
        DateTime occurredUtc,
        string agentId = null,
        string queueId = null,
        double? durationSeconds = null,
        ContactCenterActorType actorType = ContactCenterActorType.Provider,
        string state = null)
    {
        var interactionEvent = new InteractionEvent
        {
            ItemId = NextId(),
            EventType = eventType,
            AggregateType = nameof(Interaction),
            AggregateId = interactionId,
            InteractionId = interactionId,
            OccurredUtc = occurredUtc,
            RecordedUtc = occurredUtc,
            ActorType = actorType,
        };

        interactionEvent.SetData(new CallLifecycleEventData
        {
            InteractionId = interactionId,
            AgentId = agentId,
            QueueId = queueId,
            DurationSeconds = durationSeconds,
            State = state,
        });

        return interactionEvent;
    }

    public static InteractionEvent Offer(
        string eventType,
        string reservationId,
        string interactionId,
        string agentId,
        DateTime presentedUtc,
        DateTime? settledUtc = null,
        string queueId = null)
    {
        var interactionEvent = new InteractionEvent
        {
            ItemId = NextId(),
            EventType = eventType,
            AggregateType = nameof(ActivityReservation),
            AggregateId = reservationId,
            InteractionId = interactionId,
            OccurredUtc = settledUtc ?? presentedUtc,
            RecordedUtc = settledUtc ?? presentedUtc,
            ActorType = ContactCenterActorType.System,
        };

        interactionEvent.SetData(new OfferLifecycleEventData
        {
            ReservationId = reservationId,
            InteractionId = interactionId,
            AgentId = agentId,
            QueueId = queueId,
            PresentedUtc = presentedUtc,
            SettledUtc = settledUtc,
            RingSeconds = settledUtc.HasValue ? (settledUtc.Value - presentedUtc).TotalSeconds : null,
        });

        return interactionEvent;
    }

    private static string NextId()
        => "event-" + Interlocked.Increment(ref _sequence).ToString("000000", System.Globalization.CultureInfo.InvariantCulture);
}
