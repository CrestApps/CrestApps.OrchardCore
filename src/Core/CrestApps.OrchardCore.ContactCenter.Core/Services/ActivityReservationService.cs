using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using OrchardCore;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IActivityReservationService"/>.
/// </summary>
public sealed class ActivityReservationService : IActivityReservationService
{
    private readonly IActivityReservationManager _reservationManager;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityReservationService"/> class.
    /// </summary>
    /// <param name="reservationManager">The reservation manager.</param>
    /// <param name="queueItemManager">The queue item manager.</param>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="activityManager">The CRM activity manager.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="clock">The clock used to stamp reservation times.</param>
    public ActivityReservationService(
        IActivityReservationManager reservationManager,
        IQueueItemManager queueItemManager,
        IAgentProfileManager agentManager,
        IOmnichannelActivityManager activityManager,
        IContactCenterEventPublisher publisher,
        IClock clock)
    {
        _reservationManager = reservationManager;
        _queueItemManager = queueItemManager;
        _agentManager = agentManager;
        _activityManager = activityManager;
        _publisher = publisher;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<ActivityReservation> ReserveAsync(QueueItem queueItem, AgentProfile agent, int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queueItem);
        ArgumentNullException.ThrowIfNull(agent);

        var current = await _queueItemManager.FindByIdAsync(queueItem.ItemId, cancellationToken);

        if (current is null || current.Status != QueueItemStatus.Waiting)
        {
            return null;
        }

        queueItem = current;

        var now = _clock.UtcNow;
        var reservation = await _reservationManager.NewAsync(cancellationToken: cancellationToken);
        reservation.ActivityItemId = queueItem.ActivityItemId;
        reservation.QueueId = queueItem.QueueId;
        reservation.QueueItemId = queueItem.ItemId;
        reservation.AgentId = agent.ItemId;
        reservation.Status = ReservationStatus.Pending;
        reservation.CreatedUtc = now;
        reservation.ExpiresUtc = now.AddSeconds(timeoutSeconds);

        await _reservationManager.CreateAsync(reservation, cancellationToken: cancellationToken);

        queueItem.Status = QueueItemStatus.Reserved;
        queueItem.ReservationId = reservation.ItemId;
        queueItem.AgentId = agent.ItemId;
        await _queueItemManager.UpdateAsync(queueItem, cancellationToken: cancellationToken);

        agent = await _agentManager.FindByIdAsync(agent.ItemId, cancellationToken) ?? agent;

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

        await UpdateActivityAsync(queueItem.ActivityItemId, activity =>
        {
            activity.AssignmentStatus = ActivityAssignmentStatus.Reserved;
            activity.ReservationId = reservation.ItemId;
            activity.ReservedById = agent.UserId;
            activity.ReservedByUsername = agent.UserName;
            activity.ReservedUtc = now;
            activity.ReservationExpiresUtc = reservation.ExpiresUtc;
        }, cancellationToken);

        await PublishAsync(ContactCenterConstants.Events.QueueItemReserved, reservation, cancellationToken);
        await PublishAsync(ContactCenterConstants.Events.AgentReserved, reservation, cancellationToken);

        return reservation;
    }

    /// <inheritdoc/>
    public async Task<ActivityReservation> AcceptAsync(string reservationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(reservationId);

        var reservation = await _reservationManager.FindByIdAsync(reservationId, cancellationToken);

        if (reservation is null || reservation.Status != ReservationStatus.Pending)
        {
            return null;
        }

        reservation.Status = ReservationStatus.Accepted;
        await _reservationManager.UpdateAsync(reservation, cancellationToken: cancellationToken);

        var queueItem = await _queueItemManager.FindByIdAsync(reservation.QueueItemId, cancellationToken);

        if (queueItem is not null)
        {
            queueItem.Status = QueueItemStatus.Assigned;
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

        await UpdateActivityAsync(reservation.ActivityItemId, activity =>
        {
            activity.AssignmentStatus = ActivityAssignmentStatus.Assigned;
            activity.AssignedToId = agent?.UserId;
            activity.AssignedToUsername = agent?.UserName;
            activity.AssignedToUtc = _clock.UtcNow;
        }, cancellationToken);

        await PublishAsync(ContactCenterConstants.Events.QueueItemAssigned, reservation, cancellationToken);

        return reservation;
    }

    /// <inheritdoc/>
    public async Task<ActivityReservation> RejectAsync(string reservationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(reservationId);

        var reservation = await _reservationManager.FindByIdAsync(reservationId, cancellationToken);

        if (reservation is null ||
            reservation.Status is not ReservationStatus.Pending and not ReservationStatus.Accepted)
        {
            return null;
        }

        await ReleaseAsync(reservation, ReservationStatus.Rejected, cancellationToken);

        return reservation;
    }

    /// <inheritdoc/>
    public async Task<ActivityReservation> CancelAsync(string reservationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(reservationId);

        var reservation = await _reservationManager.FindByIdAsync(reservationId, cancellationToken);

        if (reservation is null || reservation.Status != ReservationStatus.Pending)
        {
            return null;
        }

        await ReleaseAsync(reservation, ReservationStatus.Canceled, cancellationToken);

        return reservation;
    }

    /// <inheritdoc/>
    public async Task<int> ExpireDueAsync(CancellationToken cancellationToken = default)
    {
        var expired = await _reservationManager.ListExpiredAsync(_clock.UtcNow, cancellationToken);
        var count = 0;

        foreach (var reservation in expired)
        {
            await ReleaseAsync(reservation, ReservationStatus.Expired, cancellationToken);
            count++;
        }

        return count;
    }

    private async Task ReleaseAsync(ActivityReservation reservation, ReservationStatus status, CancellationToken cancellationToken)
    {
        reservation.Status = status;
        await _reservationManager.UpdateAsync(reservation, cancellationToken: cancellationToken);

        var queueItem = await _queueItemManager.FindByIdAsync(reservation.QueueItemId, cancellationToken);

        if (queueItem is not null)
        {
            queueItem.Status = QueueItemStatus.Waiting;
            queueItem.ReservationId = null;
            queueItem.AgentId = null;
            await _queueItemManager.UpdateAsync(queueItem, cancellationToken: cancellationToken);
        }

        var agent = await _agentManager.FindByIdAsync(reservation.AgentId, cancellationToken);

        if (agent is not null)
        {
            agent.PresenceStatus = agent.RequestedPresenceStatus ?? AgentPresenceStatus.Available;
            agent.RequestedPresenceStatus = null;
            agent.ActiveReservationId = null;
            agent.PresenceChangedUtc = _clock.UtcNow;
            await _agentManager.UpdateAsync(agent, cancellationToken: cancellationToken);
        }

        await UpdateActivityAsync(reservation.ActivityItemId, activity =>
        {
            activity.AssignmentStatus = ActivityAssignmentStatus.Available;
            activity.ReservationId = null;
            activity.ReservedById = null;
            activity.ReservedByUsername = null;
            activity.ReservedUtc = null;
            activity.ReservationExpiresUtc = null;
        }, cancellationToken);

        await PublishAsync(ContactCenterConstants.Events.AgentReleased, reservation, cancellationToken);
    }

    private async Task UpdateActivityAsync(string activityItemId, Action<OmnichannelActivity> mutate, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(activityItemId))
        {
            return;
        }

        var activity = await _activityManager.FindByIdAsync(activityItemId, cancellationToken);

        if (activity is null)
        {
            return;
        }

        mutate(activity);

        await _activityManager.UpdateAsync(activity, cancellationToken: cancellationToken);
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
}
