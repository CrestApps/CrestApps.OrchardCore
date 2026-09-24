using System.Globalization;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Builds the call and offer audit payloads from the records the writers already hold, so every writer fills the
/// same fields the same way.
/// </summary>
internal static class ContactCenterCallAudit
{
    /// <summary>
    /// Creates a call payload describing an interaction.
    /// </summary>
    /// <param name="interaction">The interaction.</param>
    /// <returns>The payload.</returns>
    public static CallLifecycleEventData ForInteraction(Interaction interaction)
    {
        ArgumentNullException.ThrowIfNull(interaction);

        return new CallLifecycleEventData
        {
            InteractionId = interaction.ItemId,
            ActivityItemId = interaction.ActivityItemId,
            ProviderName = interaction.ProviderName,
            ProviderCallId = interaction.ProviderInteractionId,
            AgentId = interaction.AgentId,
            QueueId = interaction.QueueId,
            Direction = interaction.Direction.ToString(),
            State = interaction.Status.ToString(),
        };
    }

    /// <summary>
    /// Creates a call payload describing a call session, filling what the session does not know from its
    /// interaction.
    /// </summary>
    /// <param name="session">The call session.</param>
    /// <param name="interaction">The interaction the session belongs to, when it is loaded.</param>
    /// <returns>The payload.</returns>
    public static CallLifecycleEventData ForSession(CallSession session, Interaction interaction = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new CallLifecycleEventData
        {
            InteractionId = session.InteractionId ?? interaction?.ItemId,
            CallSessionId = session.ItemId,
            ActivityItemId = session.ActivityItemId ?? interaction?.ActivityItemId,
            ProviderName = session.ProviderName ?? interaction?.ProviderName,
            ProviderCallId = session.ProviderCallId ?? interaction?.ProviderInteractionId,
            AgentId = session.AgentId ?? interaction?.AgentId,
            QueueId = session.QueueId ?? interaction?.QueueId,
            Direction = session.Direction.ToString(),
            State = session.State.ToString(),
        };
    }

    /// <summary>
    /// Creates an offer payload.
    /// </summary>
    /// <param name="reservation">The reservation the offer is.</param>
    /// <param name="interaction">The interaction offered, when there is one.</param>
    /// <param name="agent">The agent offered the work, when loaded.</param>
    /// <param name="settledUtc">When the offer was settled, or <see langword="null"/> while it is still ringing.</param>
    /// <param name="reason">Why the offer ended the way it did, when it was not accepted.</param>
    /// <returns>The payload.</returns>
    public static OfferLifecycleEventData ForOffer(
        ActivityReservation reservation,
        Interaction interaction,
        AgentProfile agent,
        DateTime? settledUtc,
        string reason = null)
    {
        ArgumentNullException.ThrowIfNull(reservation);

        // The reservation is created the moment the offer goes to the agent, so its creation is when it started
        // ringing; nothing else records that moment.
        DateTime? presentedUtc = reservation.CreatedUtc == default ? null : reservation.CreatedUtc;

        return new OfferLifecycleEventData
        {
            ReservationId = reservation.ItemId,
            InteractionId = interaction?.ItemId,
            ActivityItemId = reservation.ActivityItemId,
            QueueId = reservation.QueueId,
            AgentId = reservation.AgentId,
            UserId = agent?.UserId,
            Channel = interaction?.Channel.ToString(),
            Reason = reason,
            PresentedUtc = presentedUtc,
            SettledUtc = settledUtc,
            RingSeconds = presentedUtc.HasValue && settledUtc.HasValue
                ? Math.Max(0, (settledUtc.Value - presentedUtc.Value).TotalSeconds)
                : null,
        };
    }

    /// <summary>
    /// Gets how long a queued call has waited in the queue it is in.
    /// </summary>
    /// <param name="queueItem">The queued call.</param>
    /// <param name="nowUtc">The time the wait is measured to.</param>
    /// <returns>The wait in seconds, never negative.</returns>
    public static double QueueWaitSeconds(QueueItem queueItem, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(queueItem);

        var enteredUtc = queueItem.QueueEnteredUtc == default
            ? queueItem.EnqueuedUtc
            : queueItem.QueueEnteredUtc;

        return enteredUtc == default
            ? 0
            : Math.Max(0, (nowUtc - enteredUtc).TotalSeconds);
    }

    /// <summary>
    /// Records a voice call entering or leaving a queue. Work on other channels is not a call and is left to the
    /// queue's own events.
    /// </summary>
    /// <param name="recorder">The audit recorder.</param>
    /// <param name="eventType">Either <see cref="ContactCenterConstants.Events.CallQueued"/> or
    /// <see cref="ContactCenterConstants.Events.CallDequeued"/>.</param>
    /// <param name="queueItem">The queued call.</param>
    /// <param name="interaction">The call's interaction.</param>
    /// <param name="occurredUtc">When the call entered or left the queue.</param>
    /// <param name="reason">Why it entered or left.</param>
    /// <param name="queueId">The queue it entered or left, when that is no longer the item's queue.</param>
    /// <param name="target">Where the call went, when it left for somewhere else.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public static Task RecordQueueChangeAsync(
        this IContactCenterAuditRecorder recorder,
        string eventType,
        QueueItem queueItem,
        Interaction interaction,
        DateTime occurredUtc,
        string reason,
        string queueId = null,
        string target = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentNullException.ThrowIfNull(queueItem);

        if (interaction is null || interaction.Channel != InteractionChannel.Voice)
        {
            return Task.CompletedTask;
        }

        queueId ??= queueItem.QueueId;

        var data = ForInteraction(interaction);
        data.QueueId = queueId;
        data.State = queueItem.Status.ToString();
        data.Reason = reason;
        data.Target = target;
        data.DurationSeconds = eventType == ContactCenterConstants.Events.CallDequeued
            ? QueueWaitSeconds(queueItem, occurredUtc)
            : null;
        data.Details["queueItemId"] = queueItem.ItemId;

        // A call enters and leaves each queue once per visit, and a visit is named by when it began, so a
        // redelivered transition collapses onto the first while a second visit to the same queue is its own record.
        var enteredUtc = queueItem.QueueEnteredUtc == default ? queueItem.EnqueuedUtc : queueItem.QueueEnteredUtc;

        return recorder.RecordCallAsync(
            eventType,
            data,
            occurredUtc,
            ContactCenterActor.System,
            $"queue:{eventType}:{queueItem.ItemId}:{queueId}:{Stamp(enteredUtc)}",
            cancellationToken);
    }

    /// <summary>
    /// Records that an interaction was created, once per interaction.
    /// </summary>
    /// <param name="recorder">The audit recorder.</param>
    /// <param name="interaction">The interaction just created.</param>
    /// <param name="reason">How it came to exist: the activity source or the path that created it.</param>
    /// <param name="actor">Who created it.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public static Task RecordInteractionCreatedAsync(
        this IContactCenterAuditRecorder recorder,
        Interaction interaction,
        string reason,
        ContactCenterActor actor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentNullException.ThrowIfNull(interaction);

        var data = ForInteraction(interaction);
        data.Reason = reason;
        data.Details["channel"] = interaction.Channel.ToString();

        return recorder.RecordCallAsync(
            ContactCenterConstants.Events.InteractionCreated,
            data,
            interaction.CreatedUtc,
            actor,
            $"interaction-created:{interaction.ItemId}",
            cancellationToken);
    }

    /// <summary>
    /// Records one phase of a consult: started, connected, completed or cancelled.
    /// </summary>
    /// <param name="recorder">The audit recorder.</param>
    /// <param name="eventType">The consult event type.</param>
    /// <param name="session">The call the consult belongs to.</param>
    /// <param name="consult">The consult.</param>
    /// <param name="occurredUtc">When the phase was reached.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public static Task RecordConsultAsync(
        this IContactCenterAuditRecorder recorder,
        string eventType,
        CallSession session,
        ConsultCall consult,
        DateTime occurredUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(consult);

        var data = ForSession(session);
        data.ProviderLegId = consult.ProviderLegId;
        data.LegRole = nameof(CallPartyRole.Consult);
        data.AgentId = consult.InitiatedByAgentId ?? data.AgentId;
        data.State = consult.Status.ToString();
        data.Target = consult.TargetAddress ?? consult.TargetId;
        data.Details["consultId"] = consult.ConsultId;
        data.Details["targetType"] = consult.TargetType.ToString();

        // A consult that ended reports how long it ran; one that connected, how long it rang.
        data.DurationSeconds = eventType == ContactCenterConstants.Events.ConsultStarted
            ? null
            : Math.Max(0, (occurredUtc - consult.StartedUtc).TotalSeconds);

        return recorder.RecordCallAsync(
            eventType,
            data,
            occurredUtc,
            string.IsNullOrEmpty(consult.InitiatedByAgentId) ? ContactCenterActor.System : new ContactCenterActor(ContactCenterActorType.Agent, consult.InitiatedByAgentId),
            $"consult:{eventType}:{consult.ConsultId}",
            cancellationToken);
    }

    /// <summary>
    /// Formats a time to the tick, for an idempotency key.
    /// </summary>
    /// <param name="value">The time.</param>
    /// <returns>The time's ticks.</returns>
    public static string Stamp(DateTime value)
        => value.Ticks.ToString(CultureInfo.InvariantCulture);
}
