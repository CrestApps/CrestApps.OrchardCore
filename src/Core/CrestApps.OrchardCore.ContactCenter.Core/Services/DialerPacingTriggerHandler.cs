using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Asks for a campaign queue to be paced when an agent may have become free to take one of its calls, or when work
/// arrives for it: an agent signs in or becomes available, an offer is released or expires, a dialer attempt or a call
/// ends, or a record is loaded into the campaign.
/// </summary>
/// <remarks>
/// It reads only the event: the queue is named by the event's payload, or derived from the campaigns an agent is signed
/// into. It injects nothing but the tenant-singleton scheduler, so constructing it never reaches the presence, queue or
/// disposition services whose own events construct the handlers. An event that only takes an agent or a record (an offer
/// accepted, a call connected) asks for nothing, so a cycle's own reservations never set off another cycle. Requests are
/// merged per queue and a replayed event only asks again.
/// </remarks>
internal sealed class DialerPacingTriggerHandler : IContactCenterEventHandler
{
    private readonly IDialerPacingScheduler _scheduler;

    /// <summary>
    /// Initializes a new instance of the <see cref="DialerPacingTriggerHandler"/> class.
    /// </summary>
    /// <param name="scheduler">The dialer pacing scheduler.</param>
    public DialerPacingTriggerHandler(IDialerPacingScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    /// <inheritdoc/>
    public string HandlerId => "ContactCenter/DialerPacingTrigger/v1";

    /// <inheritdoc/>
    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.NaturallyIdempotent;

    /// <inheritdoc/>
    public Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        foreach (var queueId in ResolveQueueIds(interactionEvent))
        {
            _scheduler.Request(queueId);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// The campaign queues an event may have freed an agent for or added work to.
    /// </summary>
    /// <param name="interactionEvent">The event.</param>
    internal static IEnumerable<string> ResolveQueueIds(InteractionEvent interactionEvent)
    {
        IEnumerable<string> queueIds;

        switch (interactionEvent.EventType)
        {
            // An agent back to ready: a call or wrap-up ended, an offer was let go, or the agent chose to be available.
            case ContactCenterConstants.Events.AgentStateChanged:
                var change = interactionEvent.GetData<AgentStateChangedEventData>();

                if (change is null || change.CurrentState != AgentPresenceStatus.Available)
                {
                    return [];
                }

                queueIds = SignedInQueues(change.QueueIds, change.CampaignIds);
                break;

            // Signing into a campaign changes no state for an agent who was already available, so it is only seen here.
            case ContactCenterConstants.Events.AgentSignedIn:
            case ContactCenterConstants.Events.AgentPresenceChanged:
                var presence = interactionEvent.GetData<AgentPresenceChangedEventData>();

                if (presence is null || presence.CurrentStatus != AgentPresenceStatus.Available)
                {
                    return [];
                }

                queueIds = SignedInQueues(presence.QueueIds, presence.CampaignIds);
                break;

            // An offer released or expired: the agent is free and the record is waiting again.
            case ContactCenterConstants.Events.AgentReleased:
                var offer = interactionEvent.GetData<OfferLifecycleEventData>();
                queueIds = [offer?.QueueId, CampaignQueueOf(offer?.CampaignId)];
                break;

            // A dialed call that ended -- no answer, busy, a machine, a failure -- frees the agent it reserved.
            case ContactCenterConstants.Events.DialerAttemptCompleted:
            case ContactCenterConstants.Events.CallEnded:
                var call = interactionEvent.GetData<CallLifecycleEventData>();
                queueIds = [call?.QueueId, CampaignQueueOf(call?.CampaignId)];
                break;

            // A record that entered the campaign's queue, such as one a load just added.
            case ContactCenterConstants.Events.QueueItemAdded:
                queueIds = [interactionEvent.GetData<QueueItemAddedEventData>()?.QueueId];
                break;

            default:
                return [];
        }

        return queueIds
            .Where(ContactCenterConstants.IsCampaignQueue)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> SignedInQueues(IEnumerable<string> queueIds, IEnumerable<string> campaignIds)
        => (queueIds ?? [])
            .Concat((campaignIds ?? []).Where(campaignId => !string.IsNullOrEmpty(campaignId)).Select(ContactCenterConstants.CampaignQueue.CreateId));

    private static string CampaignQueueOf(string campaignId)
        => string.IsNullOrEmpty(campaignId) ? null : ContactCenterConstants.CampaignQueue.CreateId(campaignId);
}
