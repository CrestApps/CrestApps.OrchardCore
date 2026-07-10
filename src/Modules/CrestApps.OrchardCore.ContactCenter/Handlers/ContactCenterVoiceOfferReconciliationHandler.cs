using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.ContactCenter.Handlers;

/// <summary>
/// Reconciles queue and reservation state when provider truth reports that a voice call ended.
/// </summary>
public sealed class ContactCenterVoiceOfferReconciliationHandler : IContactCenterEventHandler
{
    private readonly IProviderVoiceOfferSynchronizationService _offerSynchronizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContactCenterVoiceOfferReconciliationHandler"/> class.
    /// </summary>
    /// <param name="offerSynchronizationService">The offer synchronization service.</param>
    public ContactCenterVoiceOfferReconciliationHandler(IProviderVoiceOfferSynchronizationService offerSynchronizationService)
    {
        _offerSynchronizationService = offerSynchronizationService;
    }

    /// <inheritdoc/>
    public Task HandleAsync(InteractionEvent interactionEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(interactionEvent);

        if (interactionEvent.EventType != ContactCenterConstants.Events.CallEnded ||
            string.IsNullOrWhiteSpace(interactionEvent.InteractionId))
        {
            return Task.CompletedTask;
        }

        return _offerSynchronizationService.ReconcileEndedOfferAsync(interactionEvent.InteractionId, cancellationToken);
    }
}
