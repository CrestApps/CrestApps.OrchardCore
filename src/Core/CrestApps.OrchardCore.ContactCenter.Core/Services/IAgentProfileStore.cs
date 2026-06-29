using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the persistence contract for agent profiles.
/// </summary>
public interface IAgentProfileStore : ICatalog<AgentProfile>
{
    /// <summary>
    /// Finds the agent profile for the specified user.
    /// </summary>
    /// <param name="userId">The Orchard user identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching agent profile, or <see langword="null"/> when none exists.</returns>
    Task<AgentProfile> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every agent profile that is available and signed in to the specified queue.
    /// </summary>
    /// <param name="queueId">The queue identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The available agents for the queue.</returns>
    Task<IReadOnlyCollection<AgentProfile>> ListAvailableForQueueAsync(string queueId, CancellationToken cancellationToken = default);
}
