using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Projects the durable Contact Center domain events onto the real-time SignalR layer so the agent
/// desktop and supervisor dashboards stay live. The handler is read-only with respect to domain state; it
/// only enriches events and forwards them to <see cref="IContactCenterRealTimeNotifier"/>.
/// </summary>
public sealed class ContactCenterRealTimeEventHandler : IContactCenterEventHandler
{
    private readonly IContactCenterRealTimeNotifier _notifier;
    private readonly IAgentProfileManager _agentManager;
    private readonly IActivityReservationManager _reservationManager;
    private readonly IQueueItemStore _queueItemStore;
    private readonly UserManager<IUser> _userManager;
    private readonly IDisplayNameProvider _displayNameProvider;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterRealTimeEventHandler"/> class.
    /// </summary>
    /// <param name="notifier">The real-time notifier used to broadcast updates.</param>
    /// <param name="agentManager">The agent profile manager used to resolve agents.</param>
    /// <param name="reservationManager">The reservation manager used to resolve offers.</param>
    /// <param name="queueItemStore">The queue item store used to compute queue depth.</param>
    /// <param name="userManager">The user manager used to resolve Orchard users.</param>
    /// <param name="displayNameProvider">The display name provider used to render agent full names.</param>
    /// <param name="clock">The clock used to stamp notifications.</param>
    public ContactCenterRealTimeEventHandler(
        IContactCenterRealTimeNotifier notifier,
        IAgentProfileManager agentManager,
        IActivityReservationManager reservationManager,
        IQueueItemStore queueItemStore,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        IClock clock)
    {
        _notifier = notifier;
        _agentManager = agentManager;
        _reservationManager = reservationManager;
        _queueItemStore = queueItemStore;
        _userManager = userManager;
        _displayNameProvider = displayNameProvider;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        switch (interactionEvent.EventType)
        {
            case ContactCenterConstants.Events.AgentSignedIn:
            case ContactCenterConstants.Events.AgentSignedOut:
            case ContactCenterConstants.Events.AgentPresenceChanged:
                await BroadcastPresenceAsync(interactionEvent, cancellationToken);
                break;

            case ContactCenterConstants.Events.AgentReserved:
                await BroadcastOfferReceivedAsync(interactionEvent, cancellationToken);
                break;

            case ContactCenterConstants.Events.AgentReleased:
                await BroadcastOfferRevokedAsync(interactionEvent, AgentOfferRevokedReason.Released, cancellationToken);
                break;

            case ContactCenterConstants.Events.QueueItemAssigned:
                await BroadcastOfferRevokedAsync(interactionEvent, AgentOfferRevokedReason.Accepted, cancellationToken);
                break;

            case ContactCenterConstants.Events.QueueItemAdded:
            case ContactCenterConstants.Events.QueueItemDequeued:
                await BroadcastQueueStatsForItemAsync(interactionEvent.AggregateId, cancellationToken);
                break;
        }
    }

    private async Task BroadcastPresenceAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(interactionEvent.AggregateId))
        {
            return;
        }

        var profile = await _agentManager.FindByIdAsync(interactionEvent.AggregateId, cancellationToken);

        if (profile is null)
        {
            return;
        }

        await _notifier.NotifyPresenceChangedAsync(new AgentPresenceNotification
        {
            UserId = profile.UserId,
            AgentId = profile.ItemId,
            DisplayName = await GetAgentDisplayNameAsync(profile, cancellationToken),
            Status = profile.PresenceStatus.ToString(),
            Reason = profile.PresenceReason,
            QueueIds = [.. profile.QueueIds],
            ChangedUtc = profile.PresenceChangedUtc ?? interactionEvent.OccurredUtc,
        }, cancellationToken);
    }

    private async Task BroadcastOfferReceivedAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken)
    {
        var reservation = await ResolveReservationAsync(interactionEvent.AggregateId, cancellationToken);

        if (reservation is null)
        {
            return;
        }

        var agent = await _agentManager.FindByIdAsync(reservation.AgentId, cancellationToken);

        await _notifier.NotifyOfferReceivedAsync(new AgentOfferNotification
        {
            UserId = agent?.UserId,
            AgentId = reservation.AgentId,
            ReservationId = reservation.ItemId,
            ActivityItemId = reservation.ActivityItemId,
            QueueItemId = reservation.QueueItemId,
            QueueId = reservation.QueueId,
            ExpiresUtc = reservation.ExpiresUtc,
            ServerTimeUtc = _clock.UtcNow,
        }, cancellationToken);

        await BroadcastQueueStatsAsync(reservation.QueueId, cancellationToken);
    }

    private async Task<string> GetAgentDisplayNameAsync(AgentProfile agent, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(agent.UserId))
        {
            var user = await _userManager.FindByIdAsync(agent.UserId);

            if (user is not null)
            {
                var displayName = await _displayNameProvider.GetAsync(user, cancellationToken);

                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    return displayName;
                }
            }
        }

        return string.IsNullOrEmpty(agent.DisplayName) ? agent.UserName : agent.DisplayName;
    }

    private async Task BroadcastOfferRevokedAsync(InteractionEvent interactionEvent, AgentOfferRevokedReason reason, CancellationToken cancellationToken)
    {
        var reservation = await ResolveReservationAsync(interactionEvent.AggregateId, cancellationToken);

        if (reservation is null)
        {
            return;
        }

        var resolvedReason = reservation.Status == ReservationStatus.Expired
            ? AgentOfferRevokedReason.Expired
            : reason;

        var agent = await _agentManager.FindByIdAsync(reservation.AgentId, cancellationToken);

        await _notifier.NotifyOfferRevokedAsync(new AgentOfferRevokedNotification
        {
            UserId = agent?.UserId,
            AgentId = reservation.AgentId,
            ReservationId = reservation.ItemId,
            QueueId = reservation.QueueId,
            Reason = resolvedReason,
        }, cancellationToken);

        await BroadcastQueueStatsAsync(reservation.QueueId, cancellationToken);
    }

    private async Task<ActivityReservation> ResolveReservationAsync(string reservationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(reservationId))
        {
            return null;
        }

        return await _reservationManager.FindByIdAsync(reservationId, cancellationToken);
    }

    private async Task BroadcastQueueStatsForItemAsync(string queueItemId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(queueItemId))
        {
            return;
        }

        var item = await _queueItemStore.FindByIdAsync(queueItemId, cancellationToken);

        if (item is null)
        {
            return;
        }

        await BroadcastQueueStatsAsync(item.QueueId, cancellationToken);
    }

    private async Task BroadcastQueueStatsAsync(string queueId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(queueId))
        {
            return;
        }

        var waiting = await _queueItemStore.ListWaitingAsync(queueId, cancellationToken);

        await _notifier.NotifyQueueStatsChangedAsync(new QueueStatsNotification
        {
            QueueId = queueId,
            WaitingCount = waiting.Count,
            ChangedUtc = _clock.UtcNow,
        }, cancellationToken);
    }
}
