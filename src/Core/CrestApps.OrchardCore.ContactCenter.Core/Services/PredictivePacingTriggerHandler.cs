using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Asks for a campaign queue to be paced when something happens that changes how many calls it should have in flight: an
/// agent becomes free or is released, an agent is claimed for a call, a call is answered, ends or is abandoned, or an
/// attempt completes.
/// </summary>
/// <remarks>
/// It reads only the event: the queue is named by the event's payload, or derived from the campaigns an agent is signed
/// into. It injects nothing but the tenant-singleton scheduler, so constructing it never reaches the presence, queue or
/// disposition services whose own events construct the handlers. Requests are merged per queue and only an over-dialing
/// queue is paced, so it is safe to request freely; a replayed event only asks again.
/// </remarks>
public sealed class PredictivePacingTriggerHandler : IContactCenterEventHandler
{
    private readonly IPredictivePacingScheduler _scheduler;

    /// <summary>
    /// Initializes a new instance of the <see cref="PredictivePacingTriggerHandler"/> class.
    /// </summary>
    /// <param name="scheduler">The predictive pacing scheduler.</param>
    public PredictivePacingTriggerHandler(IPredictivePacingScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    /// <inheritdoc/>
    public string HandlerId => "ContactCenter/PredictivePacingTrigger/v1";

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
    /// The campaign queues an event may change the pacing of.
    /// </summary>
    /// <param name="interactionEvent">The event.</param>
    internal static IEnumerable<string> ResolveQueueIds(InteractionEvent interactionEvent)
    {
        IEnumerable<string> queueIds;

        switch (interactionEvent.EventType)
        {
            // An agent free again can take a waiting answered call or a new one.
            case ContactCenterConstants.Events.AgentStateChanged:
                var change = interactionEvent.GetData<AgentStateChangedEventData>();

                if (change is null || change.CurrentState != AgentPresenceStatus.Available)
                {
                    return [];
                }

                queueIds = (change.QueueIds ?? [])
                    .Concat((change.CampaignIds ?? []).Where(campaignId => !string.IsNullOrEmpty(campaignId)).Select(ContactCenterConstants.CampaignQueue.CreateId));
                break;

            case ContactCenterConstants.Events.AgentReleased:
            case ContactCenterConstants.Events.QueueItemAssigned:
                var offer = interactionEvent.GetData<OfferLifecycleEventData>();
                queueIds = [offer?.QueueId, CampaignQueueOf(offer?.CampaignId)];
                break;

            case ContactCenterConstants.Events.CallConnected:
            case ContactCenterConstants.Events.CallEnded:
            case ContactCenterConstants.Events.DialerAttemptCompleted:
            case ContactCenterConstants.Events.DialerLiveAnswered:
            case ContactCenterConstants.Events.DialerCallAbandoned:
            case ContactCenterConstants.Events.DialerAgentConnectClaimed:
                var call = interactionEvent.GetData<CallLifecycleEventData>();
                queueIds = [call?.QueueId, CampaignQueueOf(call?.CampaignId)];
                break;

            default:
                return [];
        }

        return queueIds
            .Where(ContactCenterConstants.IsCampaignQueue)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string CampaignQueueOf(string campaignId)
        => string.IsNullOrEmpty(campaignId) ? null : ContactCenterConstants.CampaignQueue.CreateId(campaignId);
}
