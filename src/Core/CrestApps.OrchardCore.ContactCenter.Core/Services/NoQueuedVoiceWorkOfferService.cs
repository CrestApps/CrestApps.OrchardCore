namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The default <see cref="IQueuedVoiceWorkOfferService"/> for a tenant without the Voice feature. An agent going
/// available has no queued voice work to be re-offered, because no voice work is queued.
/// </summary>
public sealed class NoQueuedVoiceWorkOfferService : IQueuedVoiceWorkOfferService
{
    /// <inheritdoc/>
    public Task<int> OfferForAgentAsync(string agentId, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    /// <inheritdoc/>
    public Task<int> OfferForUserAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult(0);
}
