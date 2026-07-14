using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Offers queued inbound voice work as soon as an agent becomes available through queue sign-in or
/// a later presence change, instead of waiting for another inbound call or the reservation-expiry cycle.
/// </summary>
public sealed class OfferQueuedVoiceWorkOnAvailabilityHandler : IContactCenterEventHandler
{
    private readonly IContactCenterScopeExecutor _scopeExecutor;

    /// <summary>
    /// Initializes a new instance of the <see cref="OfferQueuedVoiceWorkOnAvailabilityHandler"/> class.
    /// </summary>
    /// <param name="scopeExecutor">The executor used to isolate queued-work offers from outbox persistence.</param>
    public OfferQueuedVoiceWorkOnAvailabilityHandler(
        IContactCenterScopeExecutor scopeExecutor)
    {
        _scopeExecutor = scopeExecutor;
    }

    /// <inheritdoc />
    public async Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        if (interactionEvent.EventType != ContactCenterConstants.Events.AgentSignedIn &&
            interactionEvent.EventType != ContactCenterConstants.Events.AgentPresenceChanged)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(interactionEvent.AggregateId))
        {
            return;
        }

        if (interactionEvent.EventType == ContactCenterConstants.Events.AgentPresenceChanged)
        {
            var transition = interactionEvent.GetData<AgentPresenceChangedEventData>();

            if (transition?.CurrentStatus != AgentPresenceStatus.Available)
            {
                return;
            }
        }

        await _scopeExecutor.ExecuteAsync<QueuedVoiceWorkOfferScopeContext>(
            context => context.OfferForAgentAsync(interactionEvent.AggregateId, cancellationToken));
    }
}
