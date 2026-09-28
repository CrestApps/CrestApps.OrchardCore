using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterRealTimeEventHandler"/> class.
    /// </summary>
    /// <param name="notifier">The real-time notifier used to broadcast updates.</param>
    /// <param name="scopeExecutor">The executor used to isolate projections from the outbox persistence scope.</param>
    /// <param name="clock">The clock used to stamp notifications.</param>
    /// <param name="logger">The logger that records which interaction changes reached an agent's screens.</param>
    public ContactCenterRealTimeEventHandler(
        IContactCenterRealTimeNotifier notifier,
        IContactCenterScopeExecutor scopeExecutor,
        IClock clock,
        ILogger<ContactCenterRealTimeEventHandler> logger)
    {
        _notifier = notifier;
        _scopeExecutor = scopeExecutor;
        _clock = clock;
        _logger = logger;
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

            // Routing moves an agent into Reserved when an offer rings and into Busy when it is accepted, and records
            // those only as a state change, never as a presence change; without this the agent's own screens kept
            // showing the state before the call for as long as it lasted.
            case ContactCenterConstants.Events.AgentStateChanged:
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

            // The call the agent is on moved. Offers and presence alone never told the agent's screens that an
            // outbound dialer call had been placed or answered: the dial is accepted before it is placed, so the
            // refresh that acceptance caused found nothing live, and the next one came only with wrap-up.
            case ContactCenterConstants.Events.DialStarted:
            case ContactCenterConstants.Events.DialFailed:
            case ContactCenterConstants.Events.CallConnected:
            case ContactCenterConstants.Events.CallHeld:
            case ContactCenterConstants.Events.CallResumed:
            case ContactCenterConstants.Events.CallConferenceChanged:
            case ContactCenterConstants.Events.CallEnded:
            case ContactCenterConstants.Events.InteractionTransferred:
            case ContactCenterConstants.Events.AgentLegAnswered:
            case ContactCenterConstants.Events.AgentLegFailed:
                await BroadcastInteractionChangedAsync(
                    interactionEvent,
                    context.InteractionManager,
                    context.AgentManager,
                    cancellationToken);
                break;

            case ContactCenterConstants.Events.CallQualityAlertRaised:
                if (interactionEvent.GetData<CallQualityAlertNotification>() is { } alert)
                {
                    await _notifier.NotifyCallQualityAlertAsync(alert, cancellationToken);
                }

                break;
        }
    }

    private async Task BroadcastInteractionChangedAsync(
        InteractionEvent interactionEvent,
        IInteractionManager interactionManager,
        IAgentProfileManager agentManager,
        CancellationToken cancellationToken)
    {
        var interactionId = string.IsNullOrEmpty(interactionEvent.InteractionId)
            ? interactionEvent.AggregateId
            : interactionEvent.InteractionId;

        if (string.IsNullOrEmpty(interactionId))
        {
            return;
        }

        var interaction = await interactionManager.FindByIdAsync(interactionId, cancellationToken);

        // A call still in the IVR or waiting in a queue has no agent yet; its screens are told through the offer.
        if (interaction is null || string.IsNullOrEmpty(interaction.AgentId))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug(
                    "Did not push {EventType} for interaction '{InteractionId}' to an agent: the interaction {Reason}.",
                    interactionEvent.EventType,
                    interactionId.SanitizeLogValue(),
                    interaction is null ? "was not found" : "has no agent");
            }

            return;
        }

        var agent = await agentManager.FindByIdAsync(interaction.AgentId, cancellationToken);

        if (agent is null || string.IsNullOrEmpty(agent.UserId))
        {
            _logger.LogWarning(
                "Did not push {EventType} for interaction '{InteractionId}' because agent '{AgentId}' could not be resolved to a user.",
                interactionEvent.EventType,
                interactionId.SanitizeLogValue(),
                interaction.AgentId.SanitizeLogValue());

            return;
        }

        await _notifier.NotifyInteractionChangedAsync(new AgentInteractionNotification
        {
            InteractionId = interaction.ItemId,
            UserId = agent.UserId,
            AgentId = agent.ItemId,
            EventType = interactionEvent.EventType,
            Status = interaction.Status.ToString(),
            Direction = interaction.Direction.ToString(),
            ServerTimeUtc = _clock.UtcNow,
        }, cancellationToken);

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(
                "Pushed InteractionChanged to agent '{AgentId}' for {EventType}: interaction '{InteractionId}' is {Direction} {Status}.",
                agent.ItemId.SanitizeLogValue(),
                interactionEvent.EventType,
                interaction.ItemId.SanitizeLogValue(),
                interaction.Direction,
                interaction.Status);
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
