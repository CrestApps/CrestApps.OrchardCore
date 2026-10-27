using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Releasing an offer whose agent's phone could not be reached, accepted or still ringing: the caller goes where the
/// queue sends an offer nobody answered, and the agent is set Away instead of being offered the next caller.
/// </summary>
public sealed partial class ActivityReservationService
{
    /// <inheritdoc/>
    public async Task<ActivityReservation> ReleaseUnreachableAgentAsync(string reservationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(reservationId);

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            GetReservationLockKey(reservationId),
            _coordinationOptions.ReservationLockTimeout,
            _coordinationOptions.ReservationLockExpiration);

        if (!locked)
        {
            return null;
        }

        await using var acquiredLock = locker;

        var reservation = await _reservationManager.FindByIdAsync(reservationId, cancellationToken);

        // Only an offer the agent took is one they failed to be reached for: an offer still ringing rings out on its
        // own, and one already settled was decided by whatever settled it, so a redelivered failure changes nothing. A
        // direct line has no queue to decide for the caller, and a campaign's call has its own abandonment handling.
        if (reservation is null ||
            reservation.Status != ReservationStatus.Accepted ||
            ContactCenterConstants.IsDirectRoutingQueue(reservation.QueueId) ||
            ContactCenterConstants.IsCampaignQueue(reservation.QueueId))
        {
            return null;
        }

        await ReleaseAsync(reservation, ReservationStatus.Canceled, cancellationToken, agentUnreachable: true);

        await CommitTransitionAsync(
            reservation.ActivityItemId,
            reservation.AgentId,
            cancellationToken);

        _logger.LogWarning(
            "Released accepted offer '{ReservationId}' for activity '{ActivityItemId}' because agent '{AgentId}' could not be reached; the caller follows the unanswered-offer action of queue '{QueueId}' and the agent was set away.",
            reservation.ItemId.SanitizeLogValue(),
            reservation.ActivityItemId.SanitizeLogValue(),
            reservation.AgentId.SanitizeLogValue(),
            reservation.QueueId.SanitizeLogValue());

        return reservation;
    }

    /// <inheritdoc/>
    public async Task<ActivityReservation> ReleaseUnreachableOfferAsync(string reservationId, string agentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(reservationId);
        ArgumentException.ThrowIfNullOrEmpty(agentId);

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            GetReservationLockKey(reservationId),
            _coordinationOptions.ReservationLockTimeout,
            _coordinationOptions.ReservationLockExpiration);

        if (!locked)
        {
            return null;
        }

        await using var acquiredLock = locker;

        var reservation = await _reservationManager.FindByIdAsync(reservationId, cancellationToken);

        // Waiting out the ring window would only keep the caller listening to nothing: the phone it rings is not there.
        // An offer the agent already answered, declined or let ring out was settled by that, and a direct line or a
        // campaign's call has no next agent in line to hand it to.
        if (reservation is null ||
            reservation.Status != ReservationStatus.Pending ||
            !string.Equals(reservation.AgentId, agentId, StringComparison.Ordinal) ||
            ContactCenterConstants.IsDirectRoutingQueue(reservation.QueueId) ||
            ContactCenterConstants.IsCampaignQueue(reservation.QueueId))
        {
            return null;
        }

        await ReleaseAsync(reservation, ReservationStatus.Expired, cancellationToken, agentUnreachable: true);

        await CommitTransitionAsync(
            reservation.ActivityItemId,
            reservation.AgentId,
            cancellationToken);

        _logger.LogWarning(
            "Released offer '{ReservationId}' for activity '{ActivityItemId}' as unanswered because the phone of agent '{AgentId}' refused it as unavailable; the caller follows the unanswered-offer action of queue '{QueueId}' and the agent was set away.",
            reservation.ItemId.SanitizeLogValue(),
            reservation.ActivityItemId.SanitizeLogValue(),
            reservation.AgentId.SanitizeLogValue(),
            reservation.QueueId.SanitizeLogValue());

        return reservation;
    }

    /// <summary>
    /// Sets an agent whose phone could not be reached Away, through the one transition point every agent state change
    /// is recorded by.
    /// </summary>
    /// <remarks>
    /// Returning the agent to work, as every other release does, offered them the next caller too, to a phone that
    /// still could not ring. They stay Away, with the reason on their screens, until they come back themselves; the
    /// state they had asked to return to goes with the offer.
    /// </remarks>
    private async Task SetUnreachableAgentAwayAsync(
        AgentProfile agent,
        ActivityReservation reservation,
        string interactionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        agent.RequestedPresenceStatus = null;
        agent.PresenceRequestedUtc = null;
        agent.ActiveReservationId = null;
        agent.PresenceReason = AgentPresenceReasons.PhoneUnreachable;
        agent.PresenceReasonCodeId = null;

        await _stateTransitions.TransitionAsync(agent, AgentPresenceStatus.Away, new AgentStateChangeContext
        {
            Source = AgentStateChangeSources.Released,
            ReleaseReason = AgentReleaseReasons.Unreachable,
            ReasonName = AgentPresenceReasons.PhoneUnreachable,
            ReservationId = reservation.ItemId,
            InteractionId = interactionId,
            ChangedUtc = now,
        }, cancellationToken);
    }

    /// <summary>
    /// Publishes the agent's new presence, as every writer of a not-ready state does, so the agent's screens show it
    /// and routing stops offering them work.
    /// </summary>
    private Task PublishPresenceChangedAsync(
        AgentProfile agent,
        AgentPresenceStatus previousStatus,
        DateTime changedUtc,
        CancellationToken cancellationToken)
    {
        var actor = ContactCenterActor.System;
        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.AgentPresenceChanged,
            AggregateType = nameof(AgentProfile),
            AggregateId = agent.ItemId,
            ActorId = actor.Id ?? ContactCenterConstants.SystemActor,
            ActorType = actor.Type,
            SourceComponent = ContactCenterConstants.Components.Agents,
            OccurredUtc = changedUtc,
        };

        interactionEvent.SetData(new AgentPresenceChangedEventData
        {
            AgentId = agent.ItemId,
            UserId = agent.UserId,
            PreviousStatus = previousStatus,
            CurrentStatus = agent.PresenceStatus,
            RequestedStatus = agent.RequestedPresenceStatus,
            Reason = agent.PresenceReason,
            QueueIds = agent.QueueIds.ToList(),
            CampaignIds = agent.CampaignIds.ToList(),
            ChangedUtc = agent.PresenceChangedUtc ?? changedUtc,
        });

        return _publisher.PublishAsync(interactionEvent, cancellationToken);
    }
}
