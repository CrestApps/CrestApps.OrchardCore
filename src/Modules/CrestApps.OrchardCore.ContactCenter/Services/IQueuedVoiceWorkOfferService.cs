namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Re-checks an available agent's signed-in queues for already-waiting inbound voice work.
/// </summary>
public interface IQueuedVoiceWorkOfferService
{
    /// <summary>
    /// Re-checks the signed-in queues for the specified agent profile.
    /// </summary>
    /// <param name="agentId">The Contact Center agent identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of queue-offer attempts performed before the agent stopped being available.</returns>
    Task<int> OfferForAgentAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-checks the signed-in queues for the specified Orchard user.
    /// </summary>
    /// <param name="userId">The Orchard user identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of queue-offer attempts performed before the agent stopped being available.</returns>
    Task<int> OfferForUserAsync(string userId, CancellationToken cancellationToken = default);
}
