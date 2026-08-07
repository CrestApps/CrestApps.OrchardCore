namespace CrestApps.OrchardCore.ContactCenter.Services;

internal sealed class QueuedVoiceWorkOfferScopeContext
{
    private readonly IQueuedVoiceWorkOfferService _offerService;

    public QueuedVoiceWorkOfferScopeContext(IEnumerable<IQueuedVoiceWorkOfferService> offerServices)
    {
        _offerService = offerServices.FirstOrDefault();
    }

    public Task OfferForAgentAsync(string agentId, CancellationToken cancellationToken)
    {
        return _offerService?.OfferForAgentAsync(agentId, cancellationToken) ?? Task.CompletedTask;
    }
}
