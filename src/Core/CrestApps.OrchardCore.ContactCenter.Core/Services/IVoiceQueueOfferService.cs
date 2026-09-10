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

    /// <summary>
    /// Offers a specific queued inbound call directly to a specific agent (a direct-to-agent / personal-line
    /// route). When the agent is unavailable, no reservation is created and the caller applies its fallback
    /// (typically normal queue routing).
    /// </summary>
    /// <param name="activityItemId">The activity whose queued call is offered.</param>
    /// <param name="queueId">The queue the call is waiting in.</param>
    /// <param name="agentId">The agent profile the call is offered to.</param>
    /// <param name="ringTimeoutSeconds">The ring window, in seconds, for a direct-to-agent offer. When null the direct-routing default is used.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The identifier of the user the call was offered to, or <see langword="null"/> when the agent is unavailable.</returns>
    Task<string> OfferToAgentAsync(
        string activityItemId,
        string queueId,
        string agentId,
        int? ringTimeoutSeconds = null,
        CancellationToken cancellationToken = default);
}
