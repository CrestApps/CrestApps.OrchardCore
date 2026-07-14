using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
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
    private readonly IContactCenterScopeExecutor _scopeExecutor;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterRealTimeEventHandler"/> class.
    /// </summary>
    /// <param name="notifier">The real-time notifier used to broadcast updates.</param>
    /// <param name="scopeExecutor">The executor used to isolate projections from the outbox persistence scope.</param>
    /// <param name="clock">The clock used to stamp notifications.</param>
    public ContactCenterRealTimeEventHandler(
        IContactCenterRealTimeNotifier notifier,
        IContactCenterScopeExecutor scopeExecutor,
        IClock clock)
    {
        _notifier = notifier;
        _scopeExecutor = scopeExecutor;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        await _scopeExecutor.ExecuteAsync<ContactCenterRealTimeEventScopeContext>(
            context => HandleInScopeAsync(interactionEvent, context, cancellationToken));
    }

    private async Task HandleInScopeAsync(
        InteractionEvent interactionEvent,
        ContactCenterRealTimeEventScopeContext context,
        CancellationToken cancellationToken)
    {
        switch (interactionEvent.EventType)
        {
            case ContactCenterConstants.Events.AgentSignedIn:
            case ContactCenterConstants.Events.AgentSignedOut:
            case ContactCenterConstants.Events.AgentPresenceChanged:
                await BroadcastPresenceAsync(
                    interactionEvent,
                    context.AgentManager,
                    context.UserManager,
                    context.DisplayNameProvider,
                    cancellationToken);
                break;

            case ContactCenterConstants.Events.AgentReserved:
                await BroadcastOfferReceivedAsync(
                    interactionEvent,
                    context.ReservationManager,
                    context.AgentManager,
                    context.QueueItemStore,
                    context.ActivityManager,
                    cancellationToken);
                break;

            case ContactCenterConstants.Events.AgentReleased:
                await BroadcastOfferRevokedAsync(
                    interactionEvent,
                    AgentOfferRevokedReason.Released,
                    context.ReservationManager,
                    context.AgentManager,
                    context.QueueItemStore,
                    cancellationToken);
                break;

            case ContactCenterConstants.Events.QueueItemAssigned:
                await BroadcastOfferRevokedAsync(
                    interactionEvent,
                    AgentOfferRevokedReason.Accepted,
                    context.ReservationManager,
                    context.AgentManager,
                    context.QueueItemStore,
                    cancellationToken);
                break;

            case ContactCenterConstants.Events.QueueItemAdded:
            case ContactCenterConstants.Events.QueueItemDequeued:
                await BroadcastQueueStatsForItemAsync(
                    interactionEvent.AggregateId,
                    context.QueueItemStore,
                    cancellationToken);
                break;
        }
    }

    private async Task BroadcastPresenceAsync(
        InteractionEvent interactionEvent,
        IAgentProfileManager agentManager,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(interactionEvent.AggregateId))
        {
            return;
        }

        var profile = await agentManager.FindByIdAsync(interactionEvent.AggregateId, cancellationToken);

        if (profile is null)
        {
            return;
        }

        await _notifier.NotifyPresenceChangedAsync(new AgentPresenceNotification
        {
            UserId = profile.UserId,
            AgentId = profile.ItemId,
            DisplayName = await GetAgentDisplayNameAsync(profile, userManager, displayNameProvider, cancellationToken),
            Status = profile.PresenceStatus.ToString(),
            Reason = profile.PresenceReason,
            QueueIds = [.. profile.QueueIds],
            ChangedUtc = profile.PresenceChangedUtc ?? interactionEvent.OccurredUtc,
        }, cancellationToken);
    }

    private async Task BroadcastOfferReceivedAsync(
        InteractionEvent interactionEvent,
        IActivityReservationManager reservationManager,
        IAgentProfileManager agentManager,
        IQueueItemStore queueItemStore,
        IOmnichannelActivityManager activityManager,
        CancellationToken cancellationToken)
    {
        var reservation = await ResolveReservationAsync(interactionEvent.AggregateId, reservationManager, cancellationToken);

        if (reservation is null)
        {
            return;
        }

        var agent = await agentManager.FindByIdAsync(reservation.AgentId, cancellationToken);
        var activity = await activityManager.FindByIdAsync(reservation.ActivityItemId, cancellationToken);

        await _notifier.NotifyOfferReceivedAsync(new AgentOfferNotification
        {
            UserId = agent?.UserId,
            AgentId = reservation.AgentId,
            ReservationId = reservation.ItemId,
            ActivityItemId = reservation.ActivityItemId,
            AutoOpenActivity = DialerActivitySourceHelper.IsDialerSource(activity?.Source),
            QueueItemId = reservation.QueueItemId,
            QueueId = reservation.QueueId,
            ExpiresUtc = reservation.ExpiresUtc,
            ServerTimeUtc = _clock.UtcNow,
        }, cancellationToken);

        await BroadcastQueueStatsAsync(reservation.QueueId, queueItemStore, cancellationToken);
    }

    private static async Task<string> GetAgentDisplayNameAsync(
        AgentProfile agent,
        UserManager<IUser> userManager,
        IDisplayNameProvider displayNameProvider,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(agent.UserId))
        {
            var user = await userManager.FindByIdAsync(agent.UserId);

            if (user is not null)
            {
                var displayName = await displayNameProvider.GetAsync(user, cancellationToken);

                if (!string.IsNullOrWhiteSpace(displayName))
                {
                    return displayName;
                }
            }
        }

        return string.IsNullOrWhiteSpace(agent.DisplayName) ? "Unknown agent" : agent.DisplayName;
    }

    private async Task BroadcastOfferRevokedAsync(
        InteractionEvent interactionEvent,
        AgentOfferRevokedReason reason,
        IActivityReservationManager reservationManager,
        IAgentProfileManager agentManager,
        IQueueItemStore queueItemStore,
        CancellationToken cancellationToken)
    {
        var reservation = await ResolveReservationAsync(interactionEvent.AggregateId, reservationManager, cancellationToken);

        if (reservation is null)
        {
            return;
        }

        var resolvedReason = reservation.Status == ReservationStatus.Expired
            ? AgentOfferRevokedReason.Expired
            : reason;

        var agent = await agentManager.FindByIdAsync(reservation.AgentId, cancellationToken);

        await _notifier.NotifyOfferRevokedAsync(new AgentOfferRevokedNotification
        {
            UserId = agent?.UserId,
            AgentId = reservation.AgentId,
            ReservationId = reservation.ItemId,
            QueueId = reservation.QueueId,
            Reason = resolvedReason,
        }, cancellationToken);

        await BroadcastQueueStatsAsync(reservation.QueueId, queueItemStore, cancellationToken);
    }

    private static async Task<ActivityReservation> ResolveReservationAsync(
        string reservationId,
        IActivityReservationManager reservationManager,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(reservationId))
        {
            return null;
        }

        return await reservationManager.FindByIdAsync(reservationId, cancellationToken);
    }

    private async Task BroadcastQueueStatsForItemAsync(
        string queueItemId,
        IQueueItemStore queueItemStore,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(queueItemId))
        {
            return;
        }

        var item = await queueItemStore.FindByIdAsync(queueItemId, cancellationToken);

        if (item is null)
        {
            return;
        }

        await BroadcastQueueStatsAsync(item.QueueId, queueItemStore, cancellationToken);
    }

    private async Task BroadcastQueueStatsAsync(
        string queueId,
        IQueueItemStore queueItemStore,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(queueId))
        {
            return;
        }

        var waiting = await queueItemStore.ListWaitingAsync(queueId, cancellationToken);

        await _notifier.NotifyQueueStatsChangedAsync(new QueueStatsNotification
        {
            QueueId = queueId,
            WaitingCount = waiting.Count,
            ChangedUtc = _clock.UtcNow,
        }, cancellationToken);
    }
}
