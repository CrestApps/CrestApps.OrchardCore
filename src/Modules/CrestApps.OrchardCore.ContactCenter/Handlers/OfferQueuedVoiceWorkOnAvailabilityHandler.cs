using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Offers queued inbound voice work as soon as an agent becomes available through queue sign-in or
/// a later presence change, instead of waiting for another inbound call or the reservation-expiry cycle.
/// </summary>
public sealed class OfferQueuedVoiceWorkOnAvailabilityHandler : IContactCenterEventHandler
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="OfferQueuedVoiceWorkOnAvailabilityHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to lazily resolve the queued-voice offer service.</param>
    public OfferQueuedVoiceWorkOnAvailabilityHandler(
        IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
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

        // Use a fresh scope because outbox dispatch may already have pending YesSql changes in its
        // scope. Resolving the offer pipeline there can trigger a flush while querying the agent.
        await using var scope = _serviceProvider.CreateAsyncScope();
        var queuedVoiceWorkOfferService = scope.ServiceProvider.GetServices<IQueuedVoiceWorkOfferService>().FirstOrDefault();

        if (queuedVoiceWorkOfferService is null)
        {
            return;
        }

        await queuedVoiceWorkOfferService.OfferForAgentAsync(interactionEvent.AggregateId, cancellationToken);
    }
}
