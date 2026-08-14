using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IActivityReservationService"/>.
/// </summary>
public sealed class ActivityReservationService : IActivityReservationService, IActivityReservationReclaimer
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);

    // A short, positive lock wait used by the latency-sensitive reclaim path. It must be positive because the
    // distributed Redis lock provider gates its acquisition loop on a timeout-derived cancellation token: a
    // zero timeout cancels before the first attempt runs, so the lock is never even tried on Redis-backed
    // tenants. A small window (below the provider's ~100 ms first retry back-off) yields effectively a single
    // acquisition attempt on both the local and Redis providers: an uncontended lock is taken immediately, and
    // a reservation another node is already transitioning is skipped after at most this short wait instead of
    // being awaited on the admission path.
    private static readonly TimeSpan _reclaimLockWait = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// The maximum number of expired reservations materialized per drain page, so a large expiry backlog is
    /// processed in bounded batches instead of being loaded in a single unbounded query.
    /// </summary>
    private const int ExpiryPageSize = 100;

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
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly IClock _clock;
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
    /// <param name="session">The YesSql session used to commit reservation state atomically.</param>
    /// <param name="clock">The clock used to stamp reservation times.</param>
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
        IDistributedLock distributedLock,
        ISession session,
        IClock clock,
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
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ActivityReservation> ReserveAsync(QueueItem queueItem, AgentProfile agent, int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queueItem);
        ArgumentNullException.ThrowIfNull(agent);

        (var activityLocker, var activityLocked) = await _distributedLock.TryAcquireLockAsync(
            GetActivityReservationLockKey(queueItem.ActivityItemId),
            _lockTimeout,
            _lockExpiration);

        if (!activityLocked)
        {
            return null;
        }

        await using var acquiredActivityLock = activityLocker;

        (var agentLocker, var agentLocked) = await _distributedLock.TryAcquireLockAsync(
            GetAgentReservationLockKey(agent.ItemId),
            _lockTimeout,
            _lockExpiration);

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
        var availability = await _availabilityService.GetAsync(agent.ItemId, queueItem.QueueId, cancellationToken);

        if (availability?.Agent is null)
        {
            return null;
        }

        agent = availability.Agent;

        if (!string.IsNullOrWhiteSpace(agent.ActiveReservationId))
        {
            return null;
        }

        var now = _clock.UtcNow;
        var reservation = await _reservationManager.NewAsync(cancellationToken: cancellationToken);
        reservation.ActivityItemId = queueItem.ActivityItemId;
        reservation.QueueId = queueItem.QueueId;
        reservation.QueueItemId = queueItem.ItemId;
        reservation.AgentId = agent.ItemId;
        reservation.TransitionTo(ReservationStatus.Pending);
        reservation.CreatedUtc = now;
        reservation.ExpiresUtc = now.AddSeconds(timeoutSeconds);

        await _reservationManager.CreateAsync(reservation, cancellationToken: cancellationToken);

        queueItem.TransitionTo(QueueItemStatus.Reserved);
        queueItem.ReservationId = reservation.ItemId;
        queueItem.AgentId = agent.ItemId;
        await _queueItemManager.UpdateAsync(queueItem, cancellationToken: cancellationToken);

        if (!agent.RequestedPresenceStatus.HasValue &&
            agent.PresenceStatus is not AgentPresenceStatus.Available and not AgentPresenceStatus.Reserved and not AgentPresenceStatus.Busy and not AgentPresenceStatus.WrapUp)
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
            _lockTimeout,
            _lockExpiration);

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
            _lockTimeout,
            _lockExpiration);

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
        }

        var agent = await _agentManager.FindByIdAsync(reservation.AgentId, cancellationToken);

        if (agent is not null)
        {
            agent.PresenceStatus = AgentPresenceStatus.Busy;
            agent.ActiveReservationId = null;
            agent.PresenceChangedUtc = _clock.UtcNow;
            await _agentManager.UpdateAsync(agent, cancellationToken: cancellationToken);
        }

        await _workStateService.MutateAsync(reservation.ActivityItemId, workState =>
        {
            workState.TransitionTo(ActivityAssignmentStatus.Assigned);
            workState.AssignedToId = agent?.UserId;
            workState.AssignedToUsername = agent?.UserName;
            workState.AssignedToUtc = _clock.UtcNow;
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
            _lockTimeout,
            _lockExpiration);

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
            _lockTimeout,
            _lockExpiration);

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
            _lockTimeout,
            _lockExpiration);

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
            _lockTimeout,
            _lockExpiration);

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
        var now = _clock.UtcNow;
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

    /// <inheritdoc/>
    public async Task<int> ExpireDueAsync(CancellationToken cancellationToken = default)
        => await ExpireDueCoreAsync(maxReservations: null, lockWait: _lockTimeout, cancellationToken);

    /// <inheritdoc/>
    public async Task<int> ReclaimDueAsync(int maxReservations, CancellationToken cancellationToken = default)
    {
        if (maxReservations <= 0)
        {
            return 0;
        }

        return await ExpireDueCoreAsync(maxReservations, lockWait: _reclaimLockWait, cancellationToken);
    }

    private async Task<int> ExpireDueCoreAsync(int? maxReservations, TimeSpan lockWait, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var count = 0;
        var examined = 0;
        DateTime? afterExpiresUtc = null;
        var afterDocumentId = 0L;

        // Drain the expiry backlog in bounded, oldest-first pages so a spike that leaves thousands of
        // reservations expired at once is processed in fixed-size batches instead of being materialized in a
        // single unbounded query. Paging is keyset (seek) based over the stable (ExpiresUtc, DocumentId)
        // order: each page advances the cursor past the last row it observed, regardless of whether that row
        // was expired here or is currently locked by another node. Because the cursor is an absolute position
        // rather than a numeric offset, concurrent expirations or insertions elsewhere in the backlog never
        // shift the window, so a live reservation is never skipped and a block of locked candidates at the
        // front never starves the drainable ones behind them. Candidates that could not be processed this run
        // (locked, or already changed) are simply retried on the next scheduled sweep, which restarts from the
        // oldest expired reservation. The loop stops when a page is short (the backlog is exhausted), when the
        // caller-supplied reservation budget is reached, or when the run is cancelled. Callers on a
        // latency-sensitive path pass a bounded budget and a short lock wait, so the pass is strictly bounded
        // and does not block on a reservation another node is already transitioning.
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pageSize = maxReservations is int max
                ? Math.Min(ExpiryPageSize, max - examined)
                : ExpiryPageSize;

            if (pageSize <= 0)
            {
                break;
            }

            var page = await _reservationManager.GetExpiredAsync(now, afterExpiresUtc, afterDocumentId, pageSize, cancellationToken);

            foreach (var candidate in page.Reservations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                examined++;

                (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
                    GetReservationLockKey(candidate.ItemId),
                    lockWait,
                    _lockExpiration);

                if (!locked)
                {
                    continue;
                }

                await using var acquiredLock = locker;

                var reservation = await _reservationManager.FindByIdAsync(candidate.ItemId, cancellationToken);

                if (reservation is null ||
                    reservation.Status != ReservationStatus.Pending ||
                    reservation.ExpiresUtc > now)
                {
                    continue;
                }

                await ReleaseAsync(reservation, ReservationStatus.Expired, cancellationToken);
                await CommitTransitionAsync(
                    reservation.ActivityItemId,
                    reservation.AgentId,
                    cancellationToken);
                count++;
            }

            if (!page.HasMore)
            {
                break;
            }

            if (maxReservations is int budget && examined >= budget)
            {
                break;
            }

            afterExpiresUtc = page.NextAfterExpiresUtc;
            afterDocumentId = page.NextAfterDocumentId;
        }

        return count;
    }

    private async Task ReleaseAsync(ActivityReservation reservation, ReservationStatus status, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        reservation.TransitionTo(status);

        // This is the age settled reservations are purged by. Without it the row is never selected by retention.
        reservation.ModifiedUtc = now;

        await _reservationManager.UpdateAsync(reservation, cancellationToken: cancellationToken);

        var queueItem = await _queueItemManager.FindByIdAsync(reservation.QueueItemId, cancellationToken);

        if (queueItem is not null &&
            !string.IsNullOrWhiteSpace(queueItem.ReservationId) &&
            !string.Equals(queueItem.ReservationId, reservation.ItemId, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Skipped releasing expired reservation '{ReservationId}' for activity '{ActivityItemId}' because queue item '{QueueItemId}' is now owned by newer reservation '{CurrentReservationId}'.",
                reservation.ItemId.SanitizeLogValue(),
                reservation.ActivityItemId.SanitizeLogValue(),
                queueItem.ItemId.SanitizeLogValue(),
                queueItem.ReservationId.SanitizeLogValue());

            var obsoleteAgent = await _agentManager.FindByIdAsync(reservation.AgentId, cancellationToken);

            if (obsoleteAgent is not null &&
                string.Equals(obsoleteAgent.ActiveReservationId, reservation.ItemId, StringComparison.Ordinal))
            {
                obsoleteAgent.PresenceStatus = obsoleteAgent.RequestedPresenceStatus ?? AgentPresenceUtilities.ResolveDefaultReadyState(obsoleteAgent);
                obsoleteAgent.RequestedPresenceStatus = null;
                obsoleteAgent.ActiveReservationId = null;
                obsoleteAgent.PresenceChangedUtc = now;
                await _agentManager.UpdateAsync(obsoleteAgent, cancellationToken: cancellationToken);
                await PublishAsync(ContactCenterConstants.Events.AgentReleased, reservation, cancellationToken);
            }

            return;
        }

        var queue = !string.IsNullOrEmpty(reservation.QueueId)
            ? await _queueManager.FindByIdAsync(reservation.QueueId, cancellationToken)
            : null;
        var agent = await _agentManager.FindByIdAsync(reservation.AgentId, cancellationToken);
        var interaction = await _interactionManager.FindByActivityIdAsync(reservation.ActivityItemId, cancellationToken);
        var configuredUnansweredAction = status == ReservationStatus.Expired
            ? queue?.UnansweredOfferAction ?? UnansweredOfferAction.Requeue
            : UnansweredOfferAction.Requeue;
        var unansweredAction = configuredUnansweredAction;
        ProviderCommandRegistration providerCommand = null;

        if (unansweredAction is UnansweredOfferAction.Voicemail or UnansweredOfferAction.Reject)
        {
            if (interaction is null ||
                string.IsNullOrWhiteSpace(interaction.ProviderInteractionId) ||
                string.IsNullOrWhiteSpace(interaction.ProviderName) ||
                _providerCommandStateService is null)
            {
                _logger.LogWarning(
                    "The unanswered-offer action '{UnansweredOfferAction}' could not be persisted for activity '{ActivityItemId}' because provider command infrastructure or call identity is unavailable.",
                    unansweredAction,
                    interaction?.ActivityItemId.SanitizeLogValue());
                unansweredAction = UnansweredOfferAction.Requeue;
            }
            else
            {
                var commandId = IdGenerator.GenerateId();
                interaction.TechnicalMetadata[ContactCenterConstants.CommandMetadata.CommandId] = commandId;
                providerCommand = new ProviderCommandRegistration
                {
                    CommandId = commandId,
                    ProviderName = interaction.ProviderName,
                    CommandType = unansweredAction == UnansweredOfferAction.Voicemail
                        ? ProviderCommandType.SendToVoicemail
                        : ProviderCommandType.Reject,
                    ActivityItemId = reservation.ActivityItemId,
                    InteractionId = interaction.ItemId,
                    RemoveReservationFromQueueOnFailure = false,
                    RequestPayload = JsonSerializer.Serialize(new ProviderCallActionCommandRequest
                    {
                        Initiator = CallControlInitiator.System,
                        ActivityItemId = reservation.ActivityItemId,
                        InteractionId = interaction.ItemId,
                        QueueId = reservation.QueueId,
                        AgentId = reservation.AgentId,
                        AgentUserId = agent?.UserId,
                        ProviderCallId = interaction.ProviderInteractionId,
                        ReofferOnFailure = true,
                        Metadata = BuildOfferTimeoutMetadata(queue, agent),
                    }),
                };
            }
        }

        var requeue = unansweredAction == UnansweredOfferAction.Requeue;

        if (queueItem is not null)
        {
            queueItem.ReservationId = null;
            queueItem.AgentId = null;

            if (requeue)
            {
                queueItem.TransitionTo(QueueItemStatus.Waiting);
                await _queueItemManager.UpdateAsync(queueItem, cancellationToken: cancellationToken);
            }
            else
            {
                queueItem.DequeuedUtc = now;
                await _queueService.DequeueAsync(queueItem, QueueItemStatus.Removed, cancellationToken);
            }
        }

        if (agent is not null)
        {
            agent.PresenceStatus = agent.RequestedPresenceStatus ?? AgentPresenceUtilities.ResolveDefaultReadyState(agent);
            agent.RequestedPresenceStatus = null;
            agent.ActiveReservationId = null;
            agent.PresenceChangedUtc = now;
            await _agentManager.UpdateAsync(agent, cancellationToken: cancellationToken);
        }

        await _workStateService.MutateAsync(reservation.ActivityItemId, workState =>
        {
            workState.TransitionTo(requeue
                ? ActivityAssignmentStatus.Available
                : ActivityAssignmentStatus.Released);
            workState.ReservationId = null;
            workState.ReservedById = null;
            workState.ReservedByUsername = null;
            workState.ReservedUtc = null;
            workState.ReservationExpiresUtc = null;
        }, cancellationToken);

        if (!requeue)
        {
            var terminalStatus = unansweredAction == UnansweredOfferAction.Voicemail
                ? ActivityStatus.Completed
                : ActivityStatus.Cancelled;

            await _activityWriter.ScheduleUpdateAsync(reservation.ActivityItemId, activity =>
            {
                activity.Status = terminalStatus;
                activity.CompletedUtc = now;
            }, cancellationToken);
        }

        // Releasing an offer races the conversation ending. The customer can abandon while the offer is still
        // ringing an agent, the provider event settles the interaction, and this sweep then arrives to return
        // work that no longer exists to routing. Returning a settled interaction to routing is refused by the
        // lifecycle, and this path runs from a background sweep that releases every due reservation, so letting
        // that refusal escape would abandon the rest of the sweep over one call that had already hung up. The
        // reservation, queue item, agent and work state are still released; only the re-offer is skipped.
        if (interaction is not null && !interaction.IsSettled)
        {
            if (requeue)
            {
                interaction.Requeue();
            }
            else if (providerCommand is not null)
            {
                interaction.Reoffer();
                interaction.EndedUtc = null;
                interaction.AgentId = null;
                interaction.TechnicalMetadata["unansweredOfferAction"] = unansweredAction.ToString();
            }
            else
            {
                interaction.TransitionTo(InteractionStatus.Ended);
                interaction.EndedUtc ??= now;
                interaction.TechnicalMetadata["unansweredOfferAction"] = unansweredAction.ToString();
            }

            await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
        }

        await PublishAsync(ContactCenterConstants.Events.AgentReleased, reservation, cancellationToken);

        if (providerCommand is not null)
        {
            await _providerCommandStateService.RegisterAsync(providerCommand, cancellationToken);
            _scopeExecutor.ScheduleAfterCommit<IProviderCommandProcessor>(processor =>
                processor.DispatchAsync(providerCommand.CommandId, CancellationToken.None));
        }
    }

    private async Task CommitTransitionAsync(
        string activityItemId,
        string agentId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _session.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyException)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "A concurrent Contact Center operation won the compare-and-set transition for activity '{ActivityId}' and agent '{AgentId}'.",
                    activityItemId.SanitizeLogValue(),
                    agentId.SanitizeLogValue());
            }

            throw;
        }
    }

    private static Dictionary<string, object> BuildOfferTimeoutMetadata(ActivityQueue queue, AgentProfile agent)
    {
        var metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (queue is not null)
        {
            metadata["queueId"] = queue.ItemId;

            if (!string.IsNullOrWhiteSpace(queue.Name))
            {
                metadata["queueName"] = queue.Name;
            }
        }

        if (agent is not null)
        {
            if (!string.IsNullOrWhiteSpace(agent.UserId))
            {
                metadata["voicemailRecipientUserId"] = agent.UserId;
            }

            if (!string.IsNullOrWhiteSpace(agent.UserName))
            {
                metadata["voicemailRecipientUserName"] = agent.UserName;
            }

            if (!string.IsNullOrWhiteSpace(agent.DisplayName))
            {
                metadata["voicemailRecipientDisplayName"] = agent.DisplayName;
            }
        }

        return metadata;
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
