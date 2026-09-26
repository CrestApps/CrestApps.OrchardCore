using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.ContactCenter.Services;

internal sealed class QueuedVoiceWorkOfferScopeContext
{
    private readonly IQueuedVoiceWorkOfferService _offerService;

    public QueuedVoiceWorkOfferScopeContext(IQueuedVoiceWorkOfferService offerService)
    {
        _offerService = offerService;
    }

    public Task OfferForAgentAsync(string agentId, CancellationToken cancellationToken)
    {
        return _offerService.OfferForAgentAsync(agentId, cancellationToken);
    }
}
