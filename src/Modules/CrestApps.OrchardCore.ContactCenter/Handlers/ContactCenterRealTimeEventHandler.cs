using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
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
    public string HandlerId => "ContactCenter/RealTimeProjection/v1";

    /// <inheritdoc/>
    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.NaturallyIdempotent;

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

            case ContactCenterConstants.Events.AgentEntitlementsChanged:
                await BroadcastMembershipChangedAsync(
                    interactionEvent,
                    context.AgentManager,
                    cancellationToken);
                break;

            case ContactCenterConstants.Events.AgentReserved:
                await BroadcastOfferReceivedAsync(
                    interactionEvent,
                    context.ReservationManager,
                    context.AgentManager,
                    context.QueueItemStore,
                    context.ActivityManager,
                    context.InteractionManager,
                    context.IncomingCallDispatcher,
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

    private async Task BroadcastMembershipChangedAsync(
        InteractionEvent interactionEvent,
        IAgentProfileManager agentManager,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(interactionEvent.AggregateId))
        {
            return;
        }

        var profile = await agentManager.FindByIdAsync(interactionEvent.AggregateId, cancellationToken);
        var change = interactionEvent.GetData<AgentEntitlementsChangedEventData>();

        if (profile is null || change is null)
        {
            return;
        }

        await _notifier.NotifyAgentMembershipChangedAsync(
            profile.UserId,
            change.RemovedQueueIds,
            cancellationToken);
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
            RequestedStatus = profile.RequestedPresenceStatus?.ToString(),
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
        IInteractionManager interactionManager,
        IIncomingCallDispatcher incomingCallDispatcher,
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
            Kind = AgentOfferKindHelper.FromActivitySource(activity?.Source),
            QueueItemId = reservation.QueueItemId,
            QueueId = reservation.QueueId,
            ExpiresUtc = reservation.ExpiresUtc,
            ServerTimeUtc = _clock.UtcNow,
        }, cancellationToken);

        await DispatchSoftPhoneRingAsync(reservation, agent, interactionManager, incomingCallDispatcher, _clock.UtcNow, cancellationToken);

        await BroadcastQueueStatsAsync(reservation.QueueId, queueItemStore, cancellationToken);
    }

    /// <summary>
    /// Projects a ringing inbound queue offer onto the agent's soft phone as a Telephony
    /// <c>IncomingCall</c>. The reservation broadcast above only reaches Contact Center clients over the
    /// Contact Center hub, so without this the soft phone (the browser extension and the Windows app, which
    /// listen only for <c>IncomingCall</c> on the Telephony hub) never rings for queue calls -- only for
    /// direct-to-agent DID calls, which the Dialpad inbound router dispatches. The dispatcher runs the same
    /// incoming-call context providers used by the current-offer recovery poll, so the matched-customer
    /// cards and the accept/decline offer actions are attached here too, and both paths surface the same
    /// call id (the modal dedupes on it).
    /// </summary>
    private static async Task DispatchSoftPhoneRingAsync(
        ActivityReservation reservation,
        AgentProfile agent,
        IInteractionManager interactionManager,
        IIncomingCallDispatcher incomingCallDispatcher,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (incomingCallDispatcher is null ||
            agent is null ||
            string.IsNullOrEmpty(agent.UserId) ||
            string.IsNullOrEmpty(reservation.ActivityItemId))
        {
            return;
        }

        var interaction = await interactionManager.FindByActivityIdAsync(reservation.ActivityItemId, cancellationToken);

        // Ring only a genuine inbound voice call that is currently alerting the agent. A non-voice
        // reservation (or one that has already advanced past ringing) carries no ringing call to surface,
        // and the guard mirrors the one the current-offer recovery poll applies so the two agree.
        if (interaction is null ||
            interaction.Direction != InteractionDirection.Inbound ||
            interaction.Status != InteractionStatus.Ringing ||
            string.IsNullOrWhiteSpace(interaction.ProviderInteractionId))
        {
            return;
        }

        var call = ContactCenterIncomingCallFactory.BuildRingingInboundCall(interaction, nowUtc);

        await incomingCallDispatcher.DispatchAsync(agent.UserId, call, cancellationToken);
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
            ActivityItemId = reservation.ActivityItemId,
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

        var waitingCount = await queueItemStore.CountWaitingAsync(queueId, cancellationToken);

        await _notifier.NotifyQueueStatsChangedAsync(new QueueStatsNotification
        {
            QueueId = queueId,
            WaitingCount = waitingCount,
            ChangedUtc = _clock.UtcNow,
        }, cancellationToken);
    }
}
