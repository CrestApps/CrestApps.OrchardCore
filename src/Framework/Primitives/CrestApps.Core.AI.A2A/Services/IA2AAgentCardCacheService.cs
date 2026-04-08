using A2A;
using CrestApps.Core.AI.A2A.Models;

namespace CrestApps.Core.AI.A2A.Services;

/// <summary>
/// Provides cached access to agent cards from remote A2A host connections.
/// </summary>
public interface IA2AAgentCardCacheService
{
    /// <summary>
    /// Gets the agent card for the specified A2A connection, using a cached value if available.
    /// </summary>
    Task<AgentCard> GetAgentCardAsync(string connectionId, A2AConnection connection, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the cached agent card for the specified connection.
    /// </summary>
    void Invalidate(string connectionId);
}
