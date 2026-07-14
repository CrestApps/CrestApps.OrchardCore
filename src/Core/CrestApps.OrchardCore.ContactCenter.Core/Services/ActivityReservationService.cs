using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IActivityReservationService"/>.
/// </summary>
public sealed class ActivityReservationService : IActivityReservationService
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);

    private readonly IActivityReservationManager _reservationManager;
    private readonly IQueueItemManager _queueItemManager;
    private readonly IAgentProfileManager _agentManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly IActivityQueueService _queueService;
    private readonly IInteractionManager _interactionManager;
    private readonly IOmnichannelActivityManager _activityManager;
    private readonly IContactCenterEventPublisher _publisher;
    private readonly ITelephonyService _telephonyService;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityReservationService"/> class.
    /// </summary>
    /// <param name="reservationManager">The reservation manager.</param>
    /// <param name="queueItemManager">The queue item manager.</param>
    /// <param name="agentManager">The agent profile manager.</param>
    /// <param name="queueManager">The queue manager.</param>
    /// <param name="queueService">The queue service used for dequeue operations.</param>
    /// <param name="interactionManager">The interaction manager.</param>
    /// <param name="activityManager">The CRM activity manager.</param>
    /// <param name="publisher">The Contact Center event publisher.</param>
    /// <param name="telephonyServices">The optional telephony services used for voice-specific timeout actions.</param>
    /// <param name="distributedLock">The distributed lock used to serialize agent and reservation transitions.</param>
    /// <param name="clock">The clock used to stamp reservation times.</param>
    /// <param name="logger">The logger.</param>
    public ActivityReservationService(
        IActivityReservationManager reservationManager,
        IQueueItemManager queueItemManager,
        IAgentProfileManager agentManager,
        IActivityQueueManager queueManager,
        IActivityQueueService queueService,
        IInteractionManager interactionManager,
        IOmnichannelActivityManager activityManager,
        IContactCenterEventPublisher publisher,
        IEnumerable<ITelephonyService> telephonyServices,
        IDistributedLock distributedLock,
        IClock clock,
        ILogger<ActivityReservationService> logger)
    {
        _reservationManager = reservationManager;
        _queueItemManager = queueItemManager;
        _agentManager = agentManager;
        _queueManager = queueManager;
        _queueService = queueService;
        _interactionManager = interactionManager;
        _activityManager = activityManager;
        _publisher = publisher;
        _telephonyService = telephonyServices.FirstOrDefault();
        _distributedLock = distributedLock;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ActivityReservation> ReserveAsync(QueueItem queueItem, AgentProfile agent, int timeoutSeconds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(queueItem);
        ArgumentNullException.ThrowIfNull(agent);

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            GetAgentReservationLockKey(agent.ItemId),
            _lockTimeout,
            _lockExpiration);

        if (!locked)
        {
            return null;
        }

        await using var acquiredLock = locker;

        var current = await _queueItemManager.FindByIdAsync(queueItem.ItemId, cancellationToken);

        if (current is null || current.Status != QueueItemStatus.Waiting)
        {
            return null;
        }

        queueItem = current;
        agent = await _agentManager.FindByIdAsync(agent.ItemId, cancellationToken) ?? agent;

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
        reservation.Status = ReservationStatus.Pending;
        reservation.CreatedUtc = now;
        reservation.ExpiresUtc = now.AddSeconds(timeoutSeconds);

        await _reservationManager.CreateAsync(reservation, cancellationToken: cancellationToken);

        queueItem.Status = QueueItemStatus.Reserved;
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

        return reservation;
    }

    /// <inheritdoc/>
    public async Task<int> ExpireDueAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var expired = await _reservationManager.ListExpiredAsync(now, cancellationToken);
        var count = 0;

        foreach (var candidate in expired)
        {
            (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
                GetReservationLockKey(candidate.ItemId),
                _lockTimeout,
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
            count++;
        }

        return count;
    }

    private async Task ReleaseAsync(ActivityReservation reservation, ReservationStatus status, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        reservation.Status = status;
        await _reservationManager.UpdateAsync(reservation, cancellationToken: cancellationToken);

        var queueItem = await _queueItemManager.FindByIdAsync(reservation.QueueItemId, cancellationToken);

        if (queueItem is not null &&
            !string.IsNullOrWhiteSpace(queueItem.ReservationId) &&
            !string.Equals(queueItem.ReservationId, reservation.ItemId, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Skipped releasing expired reservation '{ReservationId}' for activity '{ActivityItemId}' because queue item '{QueueItemId}' is now owned by newer reservation '{CurrentReservationId}'.",
                OperationalLogRedactor.Pseudonymize(reservation.ItemId, OperationalLogIdentifierCategory.Reservation),
                OperationalLogRedactor.Pseudonymize(reservation.ActivityItemId, OperationalLogIdentifierCategory.Activity),
                OperationalLogRedactor.Pseudonymize(queueItem.ItemId, OperationalLogIdentifierCategory.Queue),
                OperationalLogRedactor.Pseudonymize(queueItem.ReservationId, OperationalLogIdentifierCategory.Reservation));

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

        if (unansweredAction is UnansweredOfferAction.Voicemail or UnansweredOfferAction.Reject &&
            !await ExecuteTimedOutOfferActionAsync(unansweredAction, interaction, queue, agent, cancellationToken))
        {
            unansweredAction = UnansweredOfferAction.Requeue;
        }

        var requeue = unansweredAction == UnansweredOfferAction.Requeue;

        if (queueItem is not null)
        {
            queueItem.ReservationId = null;
            queueItem.AgentId = null;

            if (requeue)
            {
                queueItem.Status = QueueItemStatus.Waiting;
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

        await UpdateActivityAsync(reservation.ActivityItemId, activity =>
        {
            activity.AssignmentStatus = requeue
                ? ActivityAssignmentStatus.Available
                : ActivityAssignmentStatus.Released;
            activity.ReservationId = null;
            activity.ReservedById = null;
            activity.ReservedByUsername = null;
            activity.ReservedUtc = null;
            activity.ReservationExpiresUtc = null;

            if (!requeue)
            {
                activity.Status = unansweredAction == UnansweredOfferAction.Voicemail
                    ? ActivityStatus.Completed
                    : ActivityStatus.Cancelled;
                activity.CompletedUtc = now;
            }
        }, cancellationToken);

        if (interaction is not null)
        {
            if (requeue)
            {
                interaction.Status = InteractionStatus.Created;
                interaction.AgentId = null;
            }
            else
            {
                interaction.Status = InteractionStatus.Ended;
                interaction.EndedUtc ??= now;
                interaction.TechnicalMetadata["unansweredOfferAction"] = unansweredAction.ToString();
            }

            await _interactionManager.UpdateAsync(interaction, cancellationToken: cancellationToken);
        }

        await PublishAsync(ContactCenterConstants.Events.AgentReleased, reservation, cancellationToken);
    }

    private async Task<bool> ExecuteTimedOutOfferActionAsync(
        UnansweredOfferAction unansweredAction,
        Interaction interaction,
        ActivityQueue queue,
        AgentProfile agent,
        CancellationToken cancellationToken)
    {
        if (interaction is null || string.IsNullOrWhiteSpace(interaction.ProviderInteractionId))
        {
            _logger.LogWarning(
                "The unanswered-offer action '{UnansweredOfferAction}' could not run for activity '{ActivityItemId}' because no provider interaction is available.",
                unansweredAction,
                OperationalLogRedactor.Pseudonymize(interaction?.ActivityItemId, OperationalLogIdentifierCategory.Activity));

            return false;
        }

        if (_telephonyService is null)
        {
            _logger.LogWarning(
                "The unanswered-offer action '{UnansweredOfferAction}' could not run for provider call '{ProviderCallId}' because no telephony service is registered.",
                unansweredAction,
                OperationalLogRedactor.Pseudonymize(interaction.ProviderInteractionId, OperationalLogIdentifierCategory.Call));

            return false;
        }

        var call = new CallReference
        {
            CallId = interaction.ProviderInteractionId,
            Metadata = BuildOfferTimeoutMetadata(queue, agent),
        };

        TelephonyResult result = unansweredAction switch
        {
            UnansweredOfferAction.Voicemail => await _telephonyService.SendToVoicemailAsync(call, cancellationToken),
            UnansweredOfferAction.Reject => await _telephonyService.RejectAsync(call, cancellationToken),
            _ => null,
        };

        if (result?.Succeeded == true)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "Applied the unanswered-offer action '{UnansweredOfferAction}' to provider call '{ProviderCallId}' for queue '{QueueId}'.",
                    unansweredAction,
                    OperationalLogRedactor.Pseudonymize(interaction.ProviderInteractionId, OperationalLogIdentifierCategory.Call),
                    OperationalLogRedactor.Pseudonymize(queue?.ItemId, OperationalLogIdentifierCategory.Queue));
            }

            return true;
        }

        _logger.LogWarning(
            "The unanswered-offer action '{UnansweredOfferAction}' failed for provider call '{ProviderCallId}' on queue '{QueueId}': {ErrorMessage}",
            unansweredAction,
            OperationalLogRedactor.Pseudonymize(interaction.ProviderInteractionId, OperationalLogIdentifierCategory.Call),
            OperationalLogRedactor.Pseudonymize(queue?.ItemId, OperationalLogIdentifierCategory.Queue),
            OperationalLogRedactor.Redact(result?.Error ?? "No result was returned.", OperationalLogFieldKind.FreeText));

        return false;
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

    private static string GetAgentReservationLockKey(string agentId)
    {
        return $"ContactCenterAgentReservation:{agentId}";
    }

    private static string GetReservationLockKey(string reservationId)
    {
        return $"ContactCenterReservation:{reservationId}";
    }
}
