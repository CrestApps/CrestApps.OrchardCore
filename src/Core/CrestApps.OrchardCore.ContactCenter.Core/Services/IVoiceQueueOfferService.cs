namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Reserves the next eligible agent for a queue and offers the queued inbound voice call to that agent.
/// Offering is kept as a local atomic transition so provider latency or transport failure cannot strand
/// an uncommitted reservation.
/// </summary>
public interface IVoiceQueueOfferService
{
    /// <summary>
    /// Reserves the next available agent for the queue and offers the queued inbound call to that agent.
    /// Used to route a call initially and to re-offer it after an agent declines.
    /// </summary>
    /// <param name="queueId">The queue identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The identifier of the user the call was offered to, or <see langword="null"/> when no agent is available.</returns>
    Task<string> OfferNextAsync(string queueId, CancellationToken cancellationToken = default);
}
