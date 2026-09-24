using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <inheritdoc/>
public sealed class ContactCenterAuditRecorder : IContactCenterAuditRecorder
{
    private readonly IContactCenterEventPublisher _publisher;
    private readonly IClock _clock;

    public ContactCenterAuditRecorder(
        IContactCenterEventPublisher publisher,
        IClock clock)
    {
        _publisher = publisher;
        _clock = clock;
    }

    /// <inheritdoc/>
    public Task RecordAgentStateAsync(AgentStateChangedEventData change, ContactCenterActor actor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);
        ArgumentException.ThrowIfNullOrEmpty(change.AgentId);

        var occurredUtc = Resolve(change.ChangedUtc);
        change.ChangedUtc = occurredUtc;

        var interactionEvent = Create(
            ContactCenterConstants.Events.AgentStateChanged,
            nameof(AgentProfile),
            change.AgentId,
            change.InteractionId,
            occurredUtc,
            actor,
            ContactCenterConstants.Components.Agents,
            $"agent-state:{change.AgentId}:{Stamp(occurredUtc)}:{change.PreviousState}:{change.CurrentState}");

        interactionEvent.SetData(change);

        return _publisher.PublishAsync(interactionEvent, cancellationToken);
    }

    /// <inheritdoc/>
    public Task RecordAgentSessionAsync(string eventType, AgentSessionEventData data, ContactCenterActor actor, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventType);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrEmpty(data.AgentId);

        var occurredUtc = Resolve(data.OccurredUtc);
        data.OccurredUtc = occurredUtc;

        var interactionEvent = Create(
            eventType,
            nameof(AgentProfile),
            data.AgentId,
            interactionId: null,
            occurredUtc,
            actor,
            ContactCenterConstants.Components.Agents,
            $"agent-session:{eventType}:{data.AgentId}:{data.AgentSessionId}:{data.ConnectionId}:{Stamp(occurredUtc)}");

        interactionEvent.SetData(data);

        return _publisher.PublishAsync(interactionEvent, cancellationToken);
    }

    /// <inheritdoc/>
    public Task RecordOfferAsync(string eventType, OfferLifecycleEventData data, ContactCenterActor actor, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventType);
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrEmpty(data.ReservationId);

        var occurredUtc = Resolve(data.SettledUtc ?? data.PresentedUtc ?? default);

        if (data.PresentedUtc.HasValue && data.SettledUtc.HasValue && data.RingSeconds is null)
        {
            data.RingSeconds = Math.Max(0, (data.SettledUtc.Value - data.PresentedUtc.Value).TotalSeconds);
        }

        // An offer settles once, so the reservation and the event type name the change on their own.
        var interactionEvent = Create(
            eventType,
            nameof(ActivityReservation),
            data.ReservationId,
            data.InteractionId,
            occurredUtc,
            actor,
            ContactCenterConstants.Components.Routing,
            $"offer:{eventType}:{data.ReservationId}");

        interactionEvent.SetData(data);

        return _publisher.PublishAsync(interactionEvent, cancellationToken);
    }

    /// <inheritdoc/>
    public Task RecordCallAsync(
        string eventType,
        CallLifecycleEventData data,
        DateTime occurredUtc,
        ContactCenterActor actor,
        string idempotencyKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(eventType);
        ArgumentNullException.ThrowIfNull(data);

        occurredUtc = Resolve(occurredUtc);

        var aggregateId = data.InteractionId ?? data.CallSessionId ?? data.ProviderLegId ?? data.ProviderCallId;

        var interactionEvent = Create(
            eventType,
            string.IsNullOrEmpty(data.InteractionId) ? nameof(CallSession) : nameof(Interaction),
            aggregateId,
            data.InteractionId,
            occurredUtc,
            actor,
            ContactCenterConstants.Components.CallSessions,
            idempotencyKey ?? $"call:{eventType}:{aggregateId}:{data.ProviderLegId}:{data.State}:{Stamp(occurredUtc)}");

        interactionEvent.SetData(data);

        return _publisher.PublishAsync(interactionEvent, cancellationToken);
    }

    private static InteractionEvent Create(
        string eventType,
        string aggregateType,
        string aggregateId,
        string interactionId,
        DateTime occurredUtc,
        ContactCenterActor actor,
        string sourceComponent,
        string idempotencyKey)
        => new()
        {
            EventType = eventType,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            InteractionId = interactionId,
            OccurredUtc = occurredUtc,
            ActorId = actor?.Id ?? ContactCenterConstants.SystemActor,
            ActorType = actor?.Type ?? ContactCenterActorType.System,
            SourceComponent = sourceComponent,
            IdempotencyKey = ContactCenterClaimKeys.FitIdempotencyKey(idempotencyKey),
        };

    private DateTime Resolve(DateTime occurredUtc)
        => occurredUtc == default
            ? _clock.UtcNow
            : DateTime.SpecifyKind(occurredUtc, DateTimeKind.Utc);

    // Ticks, so two changes a fraction of a second apart are two keys, never one swallowed as a duplicate.
    private static string Stamp(DateTime occurredUtc)
        => occurredUtc.Ticks.ToString(CultureInfo.InvariantCulture);
}
