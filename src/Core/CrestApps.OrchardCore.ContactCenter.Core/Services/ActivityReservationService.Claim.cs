using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Claiming a free agent for an over-dialed call a person has just answered.
/// </summary>
public sealed partial class ActivityReservationService
{
    /// <inheritdoc/>
    public async Task<ActivityReservation> ClaimConnectedCallAsync(
        QueueItem queueItem,
        AgentProfile agent,
        string interactionId,
        TimeSpan lockWait,
        Func<ActivityReservation, AgentProfile, Task> beforeCommit = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queueItem);
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentException.ThrowIfNullOrEmpty(interactionId);

        // The same two locks an inbound offer takes, in the same order, held only briefly: the person on the line is
        // waiting, and an agent another call is claiming is better skipped than waited for.
        (var activityLocker, var activityLocked) = await _distributedLock.TryAcquireLockAsync(
            GetActivityReservationLockKey(queueItem.ActivityItemId),
            lockWait,
            _coordinationOptions.ReservationLockExpiration);

        if (!activityLocked)
        {
            return null;
        }

        await using var acquiredActivityLock = activityLocker;

        (var agentLocker, var agentLocked) = await _distributedLock.TryAcquireLockAsync(
            GetAgentReservationLockKey(agent.ItemId),
            lockWait,
            _coordinationOptions.ReservationLockExpiration);

        if (!agentLocked)
        {
            return null;
        }

        await using var acquiredAgentLock = agentLocker;

        // Re-read under the locks: only a call still placed for the campaign, with nobody claimed for it, can be claimed.
        var current = await _queueItemManager.FindByIdAsync(queueItem.ItemId, cancellationToken);

        if (current is null ||
            current.Status != QueueItemStatus.Assigned ||
            !string.IsNullOrEmpty(current.AgentId) ||
            !string.IsNullOrEmpty(current.ReservationId))
        {
            return null;
        }

        var interaction = await _interactionManager.FindByIdAsync(interactionId, cancellationToken);

        if (interaction is null || interaction.IsSettled || !string.IsNullOrEmpty(interaction.AgentId))
        {
            return null;
        }

        // The availability service is the authority on whether the agent may take work now: present, Available, signed
        // into the campaign and under capacity. An agent an inbound offer or another answered call reserved in the
        // meantime is no longer available, and has an active reservation besides.
        var availability = await _availabilityService.GetAsync(agent.ItemId, current.QueueId, cancellationToken);

        if (availability?.Agent is null || !string.IsNullOrWhiteSpace(availability.Agent.ActiveReservationId))
        {
            return null;
        }

        agent = availability.Agent;

        var now = _clock.UtcNow;

        // Created already accepted: the person is on the line, so there is no offer for the agent to take or let ring
        // out, and the expiry sweep, which only reads pending reservations, never sees it.
        var reservation = await _reservationManager.NewAsync(cancellationToken: cancellationToken);
        reservation.ActivityItemId = current.ActivityItemId;
        reservation.QueueId = current.QueueId;
        reservation.QueueItemId = current.ItemId;
        reservation.DialerProfileId = current.DialerProfileId;
        reservation.AgentId = agent.ItemId;
        reservation.TransitionTo(ReservationStatus.Pending);
        reservation.TransitionTo(ReservationStatus.Accepted);
        reservation.CreatedUtc = now;
        reservation.ExpiresUtc = now;

        await _reservationManager.CreateAsync(reservation, cancellationToken: cancellationToken);

        current.ReservationId = reservation.ItemId;
        current.AgentId = agent.ItemId;
        await _queueItemManager.UpdateAsync(current, cancellationToken: cancellationToken);

        // Remember the state to return the agent to when the call ends, as an offer does.
        if (!agent.RequestedPresenceStatus.HasValue &&
            agent.PresenceStatus is not AgentPresenceStatus.Reserved and not AgentPresenceStatus.Busy and not AgentPresenceStatus.WrapUp)
        {
            agent.RequestedPresenceStatus = agent.PresenceStatus == AgentPresenceStatus.RequestBreak
                ? AgentPresenceStatus.Break
                : agent.PresenceStatus;
            agent.PresenceRequestedUtc = null;
        }

        agent.ActiveReservationId = null;
        agent.LastAssignedUtc = now;

        await _stateTransitions.TransitionAsync(agent, AgentPresenceStatus.Busy, new AgentStateChangeContext
        {
            Source = AgentStateChangeSources.Accepted,
            ReservationId = reservation.ItemId,
            InteractionId = interaction.ItemId,
            ChangedUtc = now,
        }, cancellationToken);

        await _agentManager.UpdateAsync(agent, cancellationToken: cancellationToken);

        await _workStateService.MutateAsync(current.ActivityItemId, workState =>
        {
            if (workState.AssignmentStatus != ActivityAssignmentStatus.Assigned && workState.CanTransitionTo(ActivityAssignmentStatus.Assigned))
            {
                workState.TransitionTo(ActivityAssignmentStatus.Assigned);
            }

            workState.ReservationId = reservation.ItemId;
            workState.ReservedById = agent.UserId;
            workState.ReservedByUsername = agent.UserName;
            workState.ReservedUtc = now;
            workState.ReservationExpiresUtc = null;
            workState.AssignedToId = agent.UserId;
            workState.AssignedToUsername = agent.UserName;
            workState.AssignedToUtc = now;
        }, cancellationToken);

        interaction.AgentId = agent.ItemId;
        DialerCallMetadata.MarkAgentClaimed(interaction, now);
        await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);

        // The caller connects the agent in the same transaction: the call session names the agent and the command that
        // bridges their leg is registered, so the claim and the connect commit together or not at all.
        if (beforeCommit is not null)
        {
            await beforeCommit(reservation, agent);
        }

        // The wait in queue ends here, and the agent's screens learn the call is theirs.
        await _auditRecorder.RecordQueueChangeAsync(
            ContactCenterConstants.Events.CallDequeued,
            current,
            interaction,
            now,
            CallLifecycleReasons.Assigned,
            cancellationToken: cancellationToken);

        await PublishAsync(ContactCenterConstants.Events.QueueItemAssigned, reservation, agent, ContactCenterActor.System, now, cancellationToken);

        await CommitTransitionAsync(current.ActivityItemId, agent.ItemId, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Claimed agent '{AgentId}' for over-dialed call '{InteractionId}' (activity '{ActivityItemId}') as accepted reservation '{ReservationId}'.",
                agent.ItemId.SanitizeLogValue(),
                interaction.ItemId.SanitizeLogValue(),
                current.ActivityItemId.SanitizeLogValue(),
                reservation.ItemId.SanitizeLogValue());
        }

        return reservation;
    }
}
