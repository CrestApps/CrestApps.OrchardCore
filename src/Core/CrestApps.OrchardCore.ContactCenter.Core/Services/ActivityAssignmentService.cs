using CrestApps.OrchardCore.ContactCenter.Core.Models;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IActivityAssignmentService"/>. It pairs the
/// highest-priority waiting item with the agent who has been available the longest (round robin by idle time).
/// Assignment for a queue is serialized with a per-queue distributed lock so that two nodes, or the
/// reservation-expiry background task running alongside an inbound call, cannot double-assign the same
/// item or agent.
/// </summary>
public sealed class ActivityAssignmentService : IActivityAssignmentService
{
    private static readonly TimeSpan _assignmentLockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _assignmentLockExpiration = TimeSpan.FromSeconds(30);

    private readonly IQueueItemManager _queueItemManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly IActivityRoutingService _routingService;
    private readonly IActivityReservationService _reservationService;
    private readonly IBusinessHoursService _businessHours;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityAssignmentService"/> class.
    /// </summary>
    /// <param name="queueItemManager">The queue item manager.</param>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="queueManager">The queue manager.</param>
    /// <param name="routingService">The routing service.</param>
    /// <param name="reservationService">The reservation service.</param>
    /// <param name="businessHours">The business-hours service used to pause assignment while the queue is closed.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="distributedLock">The distributed lock used to serialize assignment per queue.</param>
    /// <param name="clock">The clock used to evaluate SLA aging and business hours.</param>
    public ActivityAssignmentService(
        IQueueItemManager queueItemManager,
        IAgentProfileManager agentManager,
        IActivityQueueManager queueManager,
        IActivityRoutingService routingService,
        IActivityReservationService reservationService,
        IBusinessHoursService businessHours,
        IContactCenterEventPublisher publisher,
        IDistributedLock distributedLock,
        IClock clock)
    {
        _queueItemManager = queueItemManager;
        _agentManager = agentManager;
        _queueManager = queueManager;
        _routingService = routingService;
        _reservationService = reservationService;
        _businessHours = businessHours;
        _publisher = publisher;
        _distributedLock = distributedLock;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<ActivityReservation> AssignNextAsync(string queueId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            GetQueueLockKey(queueId),
            _assignmentLockTimeout,
            _assignmentLockExpiration);

        if (!locked)
        {
            return null;
        }

        await using var acquiredLock = locker;

        return await AssignNextCoreAsync(queueId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> AssignQueueAsync(string queueId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(queueId);

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            GetQueueLockKey(queueId),
            _assignmentLockTimeout,
            _assignmentLockExpiration);

        if (!locked)
        {
            return 0;
        }

        await using var acquiredLock = locker;

        var count = 0;

        while (await AssignNextCoreAsync(queueId, cancellationToken) is not null)
        {
            count++;
        }

        return count;
    }

    private async Task<ActivityReservation> AssignNextCoreAsync(string queueId, CancellationToken cancellationToken)
    {
        var queue = await _queueManager.FindByIdAsync(queueId, cancellationToken);

        if (queue is null || !queue.Enabled)
        {
            return null;
        }

        var now = _clock.UtcNow;

        if (!await _businessHours.IsOpenAsync(queue.BusinessHoursCalendarId, now, cancellationToken))
        {
            return null;
        }

        var waiting = await _queueItemManager.ListWaitingAsync(queueId, cancellationToken);
        var topItem = QueueItemPrioritizer.SelectNext(waiting, queue, now);

        if (topItem is null)
        {
            return null;
        }

        var agents = await _agentManager.ListAvailableForQueueAsync(queueId, cancellationToken);

        var decision = await _routingService.SelectAgentAsync(queue, topItem, agents, cancellationToken);
        await PublishRoutingDecisionAsync(decision, cancellationToken);

        if (!decision.Succeeded || decision.Agent is null)
        {
            return null;
        }

        var timeout = queue.ReservationTimeoutSeconds > 0
            ? queue.ReservationTimeoutSeconds
            : 30;

        return await _reservationService.ReserveAsync(topItem, decision.Agent, timeout, cancellationToken);
    }

    private Task PublishRoutingDecisionAsync(ActivityRoutingDecision decision, CancellationToken cancellationToken)
    {
        var data = new ActivityRoutingDecisionEventData
        {
            QueueId = decision.Queue?.ItemId,
            QueueItemId = decision.QueueItem?.ItemId,
            ActivityItemId = decision.QueueItem?.ActivityItemId,
            SelectedAgentId = decision.Agent?.ItemId,
            Succeeded = decision.Succeeded,
            Reason = decision.Reason,
            Candidates = decision.Candidates
                .Select(candidate => new ActivityRoutingCandidateDecisionData
                {
                    AgentId = candidate.Agent.ItemId,
                    UserId = candidate.Agent.UserId,
                    IsEligible = candidate.IsEligible,
                    Score = candidate.Score,
                    Reasons = [.. candidate.Reasons],
                })
                .ToArray(),
        };

        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.RoutingDecisionMade,
            AggregateType = nameof(QueueItem),
            AggregateId = decision.QueueItem?.ItemId,
            ActorId = decision.Agent?.ItemId,
            SourceComponent = ContactCenterConstants.Components.Routing,
        };

        interactionEvent.SetData(data);

        return _publisher.PublishAsync(interactionEvent, cancellationToken);
    }

    private static string GetQueueLockKey(string queueId)
    {
        return $"ContactCenterQueueAssignment:{queueId}";
    }
}
