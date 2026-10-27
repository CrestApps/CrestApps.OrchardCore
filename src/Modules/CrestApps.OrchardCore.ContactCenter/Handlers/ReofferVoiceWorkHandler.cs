using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Continues inbound routing through durable outbox delivery after an offer was declined, requeued, or rang out back into its queue.
/// </summary>
public sealed class ReofferVoiceWorkHandler : IContactCenterEventHandler
{
    private readonly Lazy<IInboundVoiceService> _inboundVoiceService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReofferVoiceWorkHandler"/> class.
    /// </summary>
    /// <param name="inboundVoiceService">
    /// The inbound voice service, resolved lazily. It is a declared dependency rather than something fetched from
    /// the container, but it stays behind <see cref="Lazy{T}"/> because inbound routing publishes events and so
    /// depends on the publisher that constructs this handler; constructing it eagerly would close that cycle.
    /// </param>
    public ReofferVoiceWorkHandler(Lazy<IInboundVoiceService> inboundVoiceService)
    {
        _inboundVoiceService = inboundVoiceService;
    }

    /// <inheritdoc/>
    public string HandlerId => "ContactCenter/ReofferVoiceWork/v1";

    /// <inheritdoc/>
    public ContactCenterHandlerReplaySafety ReplaySafety => ContactCenterHandlerReplaySafety.GuardedByDurableStore;

    /// <inheritdoc/>
    public async Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        var queueId = interactionEvent.EventType switch
        {
            ContactCenterConstants.Events.OfferDeclined or ContactCenterConstants.Events.OfferRequeued
                => interactionEvent.GetData<OfferDeclinedEventData>()?.QueueId,
            ContactCenterConstants.Events.OfferExpired => GetRequeuedQueueId(interactionEvent.GetData<OfferLifecycleEventData>()),
            _ => null,
        };

        if (string.IsNullOrEmpty(queueId))
        {
            return;
        }

        await _inboundVoiceService.Value.OfferNextAsync(queueId, cancellationToken);
    }

    /// <summary>
    /// The queue an offer that rang out went back into, when it did. Without this the next agent in line was rung only at
    /// the minute's assignment pass, and an unanswered caller often hung up before it ran. A direct line rings only the
    /// agent it belongs to, and a campaign's virtual queue is paced by the dialer, so neither has a next agent to offer.
    /// </summary>
    private static string GetRequeuedQueueId(OfferLifecycleEventData data)
        => data is not null &&
            string.Equals(data.Reason, nameof(UnansweredOfferAction.Requeue), StringComparison.Ordinal) &&
            !string.IsNullOrEmpty(data.QueueId) &&
            !ContactCenterConstants.IsDirectRoutingQueue(data.QueueId) &&
            !ContactCenterConstants.IsCampaignQueue(data.QueueId)
                ? data.QueueId
                : null;
}
