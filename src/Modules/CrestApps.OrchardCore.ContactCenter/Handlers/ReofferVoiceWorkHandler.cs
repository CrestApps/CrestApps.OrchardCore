using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Continues inbound routing after a declined offer through durable outbox delivery.
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

        if (interactionEvent.EventType != ContactCenterConstants.Events.OfferDeclined &&
            interactionEvent.EventType != ContactCenterConstants.Events.OfferRequeued)
        {
            return;
        }

        var data = interactionEvent.GetData<OfferDeclinedEventData>();

        if (string.IsNullOrEmpty(data?.QueueId))
        {
            return;
        }

        await _inboundVoiceService.Value.OfferNextAsync(data.QueueId, cancellationToken);
    }
}
