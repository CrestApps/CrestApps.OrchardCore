namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Repairs orphaned queue, reservation, and interaction state for an agent so stale records do not keep
/// blocking future routing.
/// </summary>
public interface IAgentWorkStateHealingService
{
    /// <summary>
    /// Repairs stale state before an explicitly requested sign-in or sign-out resets the agent.
    /// </summary>
    /// <param name="agentId">The agent profile identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of stale state fragments that were healed.</returns>
    Task<int> HealForResetAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Repairs stale state before queued inbound work is re-offered to an available agent.
    /// </summary>
    /// <param name="agentId">The agent profile identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of stale state fragments that were healed.</returns>
    Task<int> HealForAvailabilityAsync(string agentId, CancellationToken cancellationToken = default);
}
