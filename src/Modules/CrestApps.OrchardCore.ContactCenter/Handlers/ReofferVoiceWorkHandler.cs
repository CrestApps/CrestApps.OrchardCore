using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Continues inbound routing after a declined offer through durable outbox delivery.
/// </summary>
public sealed class ReofferVoiceWorkHandler : IContactCenterEventHandler
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReofferVoiceWorkHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve inbound routing lazily without creating an event-publisher cycle.</param>
    public ReofferVoiceWorkHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
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

        await _serviceProvider
            .GetRequiredService<IInboundVoiceService>()
            .OfferNextAsync(data.QueueId, cancellationToken);
    }
}
