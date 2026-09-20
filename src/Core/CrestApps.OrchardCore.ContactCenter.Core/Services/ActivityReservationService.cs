using CrestApps.Core.ContactCenter;
using CrestApps.Core.Omnichannel.Models;
using CrestApps.Core.Omnichannel.Services;
using System.Text.Json;
using CrestApps.Core.Locking;
using CrestApps.Core.Support;
using CrestApps.Core.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.Core.ContactCenter.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore;
using OrchardCore.Modules;
using YesSql;
using CrestApps.Core.Services;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IActivityReservationService"/>.
/// </summary>
public sealed partial class ActivityReservationService : IActivityReservationService, IActivityReservationReclaimer
{
    private readonly IActivityReservationManager _reservationManager;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly IAgentAvailabilityService _availabilityService;
    private readonly IActivityQueueManager _queueManager;
    private readonly IActivityQueueService _queueService;
    private readonly IInteractionManager _interactionManager;
    private readonly IContactCenterWorkStateService _workStateService;
    private readonly IContactCenterActivityWriter _activityWriter;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly IProviderCommandStateService _providerCommandStateService;
    private readonly IContactCenterScopeExecutor _scopeExecutor;
    private readonly IDistributedLockProvider _distributedLock;
    private readonly IStoreCommitter _storeCommitter;
    private readonly TimeProvider _timeProvider;
    private readonly ContactCenterCoordinationOptions _coordinationOptions;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityReservationService"/> class.
    /// </summary>
    /// <param name="reservationManager">The reservation manager.</param>
    /// <param name="queueItemManager">The queue item manager.</param>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="availabilityService">The canonical agent availability service.</param>
    /// <param name="queueManager">The queue manager.</param>
    /// <param name="queueService">The queue service used for dequeue operations.</param>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="workStateService">The routing-owned work state service.</param>
    /// <param name="activityWriter">The writer used to apply CRM activity lifecycle changes outside the routing transaction.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="providerCommandStateServices">The optional durable provider-command service used for voice-specific timeout actions.</param>
    /// <param name="scopeExecutor">The executor used to wake provider-command processing after commit.</param>
    /// <param name="distributedLock">The distributed lock used to serialize agent and reservation transitions.</param>
    /// <param name="storeCommitter">The commit boundary, used to commit reservation state atomically.</param>
    /// <param name="timeProvider">The time provider used to stamp reservation times.</param>
    /// <param name="logger">The logger.</param>
    public ActivityReservationService(
        IActivityReservationManager reservationManager,
        IQueueItemManager queueItemManager,
        IAgentProfileManager agentManager,
        IAgentAvailabilityService availabilityService,
        IActivityQueueManager queueManager,
        IActivityQueueService queueService,
        IInteractionManager interactionManager,
        IContactCenterWorkStateService workStateService,
        IContactCenterActivityWriter activityWriter,
        IContactCenterEventPublisher publisher,
        IEnumerable<IProviderCommandStateService> providerCommandStateServices,
        IContactCenterScopeExecutor scopeExecutor,
        IDistributedLockProvider distributedLock,
        IStoreCommitter storeCommitter,
        TimeProvider timeProvider,
        IOptions<ContactCenterCoordinationOptions> coordinationOptions,
        ILogger<ActivityReservationService> logger)
    {
        _reservationManager = reservationManager;
        _queueItemManager = queueItemManager;
        _agentManager = agentManager;
        _availabilityService = availabilityService;
        _queueManager = queueManager;
        _queueService = queueService;
        _interactionManager = interactionManager;
        _workStateService = workStateService;
        _activityWriter = activityWriter;
        _publisher = publisher;
        _providerCommandStateService = providerCommandStateServices.FirstOrDefault();
        _scopeExecutor = scopeExecutor;
        _distributedLock = distributedLock;
        _storeCommitter = storeCommitter;
        _timeProvider = timeProvider;
        _coordinationOptions = coordinationOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ActivityReservation> ReserveAsync(QueueItem queueItem, AgentProfile agent, int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queueItem);
        ArgumentNullException.ThrowIfNull(agent);

        (var activityLocker, var activityLocked) = await _distributedLock.TryAcquireLockAsync(
            GetActivityReservationLockKey(queueItem.ActivityItemId),
            _coordinationOptions.ReservationLockTimeout,
            _coordinationOptions.ReservationLockExpiration,
            cancellationToken);

        if (!activityLocked)
        {
            return null;
        }

        await using var acquiredActivityLock = activityLocker;

        (var agentLocker, var agentLocked) = await _distributedLock.TryAcquireLockAsync(
            GetAgentReservationLockKey(agent.ItemId),
            _coordinationOptions.ReservationLockTimeout,
            _coordinationOptions.ReservationLockExpiration,
            cancellationToken);

        if (!agentLocked)
        {
            return null;
        }

        await using var acquiredAgentLock = agentLocker;

        var current = await _queueItemManager.FindByIdAsync(queueItem.ItemId, cancellationToken);

        if (current is null || current.Status != QueueItemStatus.Waiting)
        {
            return null;
        }

        queueItem = current;

        // The availability service is the canonical authority for whether an agent may take work, and answering
        // that question already costs a read of the agent profile and a count of that agent's active
        // interactions. Reading the agent and counting again here would double the round trips this critical
        // section holds two distributed locks across, in order to re-derive a decision that already has an
        // owner. The locks carry a fixed expiration and are never renewed, so the length of this section is
        // what decides how often it outruns its lease; what makes that survivable is the version check the
        // commit below runs under, not the lease.
        // A direct-to-agent (personal line) reservation is carried under the synthetic direct-routing queue,
        // which has no queue membership to check. Its availability is evaluated directly against the named
        // agent so the offer does not require the agent to be entitled to, or signed into, any queue.
        var availability = ContactCenterConstants.IsDirectRoutingQueue(queueItem.QueueId)
            ? await _availabilityService.GetForDirectAsync(agent.ItemId, cancellationToken)
            : await _availabilityService.GetAsync(agent.ItemId, queueItem.QueueId, cancellationToken);

        if (availability?.Agent is null)
        {
            return null;
        }

        agent = availability.Agent;

        if (!string.IsNullOrWhiteSpace(agent.ActiveReservationId))
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var reservation = await _reservationManager.NewAsync(cancellationToken: cancellationToken);
        reservation.ActivityItemId = queueItem.ActivityItemId;
        reservation.QueueId = queueItem.QueueId;
        reservation.QueueItemId = queueItem.ItemId;
        reservation.DialerProfileId = queueItem.DialerProfileId;
        reservation.AgentId = agent.ItemId;
        reservation.TransitionTo(ReservationStatus.Pending);
        reservation.CreatedUtc = now;
        reservation.ExpiresUtc = now.AddSeconds(timeoutSeconds);

        await _reservationManager.CreateAsync(reservation, cancellationToken: cancellationToken);

        queueItem.TransitionTo(QueueItemStatus.Reserved);
        queueItem.ReservationId = reservation.ItemId;
        queueItem.AgentId = agent.ItemId;
        await _queueItemManager.UpdateAsync(queueItem, cancellationToken: cancellationToken);

        // Remember the state to return the agent to when this reservation ends. Available is captured
        // explicitly (rather than left to the post-call default) so an agent who takes a call returns to
        // Available even when they belong to no queue -- a direct-to-agent (personal line) agent. Without this,
        // the queue-membership-based default signs a queueless agent out after every direct call. The transient
        // in-call states are still not captured, so a second concurrent reservation cannot overwrite the real
        // return state.
        if (!agent.RequestedPresenceStatus.HasValue &&
            agent.PresenceStatus is not AgentPresenceStatus.Reserved and not AgentPresenceStatus.Busy and not AgentPresenceStatus.WrapUp)
        {
            agent.RequestedPresenceStatus = agent.PresenceStatus == AgentPresenceStatus.RequestBreak
                ? AgentPresenceStatus.Break
                : agent.PresenceStatus;
        }

        agent.PresenceStatus = AgentPresenceStatus.Reserved;
        agent.ActiveReservationId = reservation.ItemId;
        agent.PresenceChangedUtc = now;
        agent.LastAssignedUtc = now;
        await _agentManager.UpdateAsync(agent, cancellationToken: cancellationToken);

        await _workStateService.MutateAsync(queueItem.ActivityItemId, workState =>
        {
            workState.TransitionTo(ActivityAssignmentStatus.Reserved);
            workState.ReservationId = reservation.ItemId;
            workState.ReservedById = agent.UserId;
            workState.ReservedByUsername = agent.UserName;
            workState.ReservedUtc = now;
            workState.ReservationExpiresUtc = reservation.ExpiresUtc;
        }, cancellationToken);

        await PublishAsync(ContactCenterConstants.Events.QueueItemReserved, reservation, cancellationToken);
        await PublishAsync(ContactCenterConstants.Events.AgentReserved, reservation, cancellationToken);

        await CommitTransitionAsync(
            queueItem.ActivityItemId,
            agent.ItemId,
            cancellationToken);

        return reservation;
    }

    /// <inheritdoc/>
    public async Task<ActivityReservation> AcceptAsync(string reservationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(reservationId);

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            GetReservationLockKey(reservationId),
            _coordinationOptions.ReservationLockTimeout,
            _coordinationOptions.ReservationLockExpiration,
            cancellationToken);

        if (!locked)
        {
            return null;
        }

        await using var acquiredLock = locker;

        var reservation = await _reservationManager.FindByIdAsync(reservationId, cancellationToken);

        if (reservation is null || reservation.Status != ReservationStatus.Pending)
        {
            return null;
        }

        (var agentLocker, var agentLocked) = await _distributedLock.TryAcquireLockAsync(
            GetAgentReservationLockKey(reservation.AgentId),
            _coordinationOptions.ReservationLockTimeout,
            _coordinationOptions.ReservationLockExpiration,
            cancellationToken);

        if (!agentLocked)
        {
            return null;
        }

        await using var acquiredAgentLock = agentLocker;

        reservation.TransitionTo(ReservationStatus.Accepted);
        await _reservationManager.UpdateAsync(reservation, cancellationToken: cancellationToken);

        var queueItem = await _queueItemManager.FindByIdAsync(reservation.QueueItemId, cancellationToken);

        if (queueItem is not null)
        {
            queueItem.TransitionTo(QueueItemStatus.Assigned);
            await _queueItemManager.UpdateAsync(queueItem, cancellationToken: cancellationToken);

            // The caller has stopped waiting, so the queue stops playing to them. An assigned item is never
            // dequeued — that is what a completed or abandoned one is — so this is the only point at which the
            // hold music, started on an infinite loop, is turned off for a call that went the way it should.
            // Without it the agent introduced themselves over the music the caller had been listening to.
            await _queueService.StopHoldMusicAsync(queueItem, cancellationToken);
        }

        var agent = await _agentManager.FindByIdAsync(reservation.AgentId, cancellationToken);

        if (agent is not null)
        {
            agent.PresenceStatus = AgentPresenceStatus.Busy;
            agent.ActiveReservationId = null;
            agent.PresenceChangedUtc = _timeProvider.GetUtcNow().UtcDateTime;
            await _agentManager.UpdateAsync(agent, cancellationToken: cancellationToken);
        }

        await _workStateService.MutateAsync(reservation.ActivityItemId, workState =>
        {
            workState.TransitionTo(ActivityAssignmentStatus.Assigned);
            workState.AssignedToId = agent?.UserId;
            workState.AssignedToUsername = agent?.UserName;
            workState.AssignedToUtc = _timeProvider.GetUtcNow().UtcDateTime;
        }, cancellationToken);

        await PublishAsync(ContactCenterConstants.Events.QueueItemAssigned, reservation, cancellationToken);

        await CommitTransitionAsync(
            reservation.ActivityItemId,
            reservation.AgentId,
            cancellationToken);

        return reservation;
    }

    /// <inheritdoc/>
    public async Task<ActivityReservation> RejectAsync(string reservationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(reservationId);

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            GetReservationLockKey(reservationId),
            _coordinationOptions.ReservationLockTimeout,
            _coordinationOptions.ReservationLockExpiration,
            cancellationToken);

        if (!locked)
        {
            return null;
        }

        await using var acquiredLock = locker;

        var reservation = await _reservationManager.FindByIdAsync(reservationId, cancellationToken);

        if (reservation is null ||
            reservation.Status is not ReservationStatus.Pending and not ReservationStatus.Accepted)
        {
            return null;
        }

        await ReleaseAsync(reservation, ReservationStatus.Rejected, cancellationToken);

        await CommitTransitionAsync(
            reservation.ActivityItemId,
            reservation.AgentId,
            cancellationToken);

        return reservation;
    }

    /// <inheritdoc/>
    public async Task<ActivityReservation> CancelAsync(string reservationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(reservationId);

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            GetReservationLockKey(reservationId),
            _coordinationOptions.ReservationLockTimeout,
            _coordinationOptions.ReservationLockExpiration,
            cancellationToken);

        if (!locked)
        {
            return null;
        }

        await using var acquiredLock = locker;

        var reservation = await _reservationManager.FindByIdAsync(reservationId, cancellationToken);

        if (reservation is null || reservation.Status != ReservationStatus.Pending)
        {
            return null;
        }

        await ReleaseAsync(reservation, ReservationStatus.Canceled, cancellationToken);

        await CommitTransitionAsync(
            reservation.ActivityItemId,
            reservation.AgentId,
            cancellationToken);

        return reservation;
    }

    /// <inheritdoc/>
    public async Task<ActivityReservation> CompensateAsync(
        string reservationId,
        bool removeFromQueue,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(reservationId);

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            GetReservationLockKey(reservationId),
            _coordinationOptions.ReservationLockTimeout,
            _coordinationOptions.ReservationLockExpiration,
            cancellationToken);

        if (!locked)
        {
            return null;
        }

        await using var acquiredLock = locker;

        var reservation = await _reservationManager.FindByIdAsync(reservationId, cancellationToken);

        if (reservation is null ||
            reservation.Status is not ReservationStatus.Pending and not ReservationStatus.Accepted)
        {
            return null;
        }

        (var agentLocker, var agentLocked) = await _distributedLock.TryAcquireLockAsync(
            GetAgentReservationLockKey(reservation.AgentId),
            _coordinationOptions.ReservationLockTimeout,
            _coordinationOptions.ReservationLockExpiration,
            cancellationToken);

        if (!agentLocked)
        {
            return null;
        }

        await using var acquiredAgentLock = agentLocker;

        var activeAgentReservations = await _reservationManager.GetActiveByAgentAsync(
            reservation.AgentId,
            cancellationToken);
        var hasNewerAgentWork = activeAgentReservations.Any(candidate =>
            !string.Equals(candidate.ItemId, reservation.ItemId, StringComparison.Ordinal));
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var wasAccepted = reservation.Status == ReservationStatus.Accepted;
        reservation.TransitionTo(ReservationStatus.Canceled);

        // This is the age settled reservations are purged by. Without it the row is never selected by retention.
        reservation.ModifiedUtc = now;

        await _reservationManager.UpdateAsync(reservation, cancellationToken: cancellationToken);

        var queueItem = await _queueItemManager.FindByIdAsync(reservation.QueueItemId, cancellationToken);

        if (queueItem is not null &&
            string.Equals(queueItem.ReservationId, reservation.ItemId, StringComparison.Ordinal))
        {
            queueItem.ReservationId = null;
            queueItem.AgentId = null;

            if (removeFromQueue)
            {
                await _queueService.DequeueAsync(queueItem, QueueItemStatus.Removed, cancellationToken);
            }
            else
            {
                queueItem.TransitionTo(QueueItemStatus.Waiting);
                await _queueItemManager.UpdateAsync(queueItem, cancellationToken: cancellationToken);
            }
        }

        var agent = await _agentManager.FindByIdAsync(reservation.AgentId, cancellationToken);
        var agentReleased = false;
        var ownsPendingReservation = !wasAccepted &&
            agent?.PresenceStatus == AgentPresenceStatus.Reserved &&
            string.Equals(agent.ActiveReservationId, reservation.ItemId, StringComparison.Ordinal);
        var ownsAcceptedReservation = wasAccepted &&
            agent?.PresenceStatus == AgentPresenceStatus.Busy &&
            (string.Equals(agent.ActiveReservationId, reservation.ItemId, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(agent.ActiveReservationId));

        if (agent is not null &&
            !hasNewerAgentWork &&
            (ownsPendingReservation || ownsAcceptedReservation))
        {
            agent.PresenceStatus = agent.RequestedPresenceStatus ?? AgentPresenceUtilities.ResolveDefaultReadyState(agent);
            agent.RequestedPresenceStatus = null;
            agent.ActiveReservationId = null;
            agent.PresenceChangedUtc = now;
            await _agentManager.UpdateAsync(agent, cancellationToken: cancellationToken);
            agentReleased = true;
        }

        await _workStateService.MutateAsync(reservation.ActivityItemId, workState =>
        {
            if (!string.Equals(workState.ReservationId, reservation.ItemId, StringComparison.Ordinal))
            {
                return;
            }

            workState.TransitionTo(removeFromQueue
                ? ActivityAssignmentStatus.Released
                : ActivityAssignmentStatus.Available);
            workState.ReservationId = null;
            workState.ReservedById = null;
            workState.ReservedByUsername = null;
            workState.ReservedUtc = null;
            workState.ReservationExpiresUtc = null;
            workState.AssignedToId = null;
            workState.AssignedToUsername = null;
            workState.AssignedToUtc = null;
        }, cancellationToken);

        if (agentReleased)
        {
            await PublishAsync(ContactCenterConstants.Events.AgentReleased, reservation, cancellationToken);
        }

        await CommitTransitionAsync(
            reservation.ActivityItemId,
            reservation.AgentId,
            cancellationToken);

        return reservation;
    }

    private Task PublishAsync(string eventType, ActivityReservation reservation, CancellationToken cancellationToken)
    {
        return _publisher.PublishAsync(new InteractionEvent
        {
            EventType = eventType,
            AggregateType = nameof(ActivityReservation),
            AggregateId = reservation.ItemId,
            ActorId = reservation.AgentId,
            SourceComponent = ContactCenterConstants.Components.Queues,
        }, cancellationToken);
    }

    private static string GetAgentReservationLockKey(string agentId)
    {
        return $"ContactCenterAgentReservation:{agentId}";
    }

    private static string GetActivityReservationLockKey(string activityItemId)
    {
        return $"ContactCenterActivityReservation:{activityItemId}";
    }

    private static string GetReservationLockKey(string reservationId)
    {
        return $"ContactCenterReservation:{reservationId}";
    }
}
