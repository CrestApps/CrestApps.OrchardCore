using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the management contract for interactions.
/// </summary>
public interface IInteractionManager : ICatalogManager<Interaction>
{
    /// <summary>
    /// Finds the interaction linked to the specified CRM activity.
    /// </summary>
    /// <param name="activityItemId">The CRM activity identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching interaction, or <see langword="null"/> when none is found.</returns>
    Task<Interaction> FindByActivityIdAsync(string activityItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the interaction with the specified correlation identifier.
    /// </summary>
    /// <param name="correlationId">The correlation identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching interaction, or <see langword="null"/> when none is found.</returns>
    Task<Interaction> FindByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the most recent interaction with the specified provider interaction or call identifier.
    /// </summary>
    /// <param name="providerInteractionId">The provider interaction or call identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching interaction, or <see langword="null"/> when none is found.</returns>
    Task<Interaction> FindByProviderInteractionIdAsync(string providerInteractionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pages interactions that are currently in the specified status.
    /// </summary>
    /// <param name="page">The page number to load.</param>
    /// <param name="pageSize">The number of entries per page.</param>
    /// <param name="status">The status to filter by.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The page of matching interactions.</returns>
    Task<PageResult<Interaction>> PageByStatusAsync(int page, int pageSize, InteractionStatus status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the active (not ended and not failed) interactions currently connected to the specified agent.
    /// </summary>
    /// <param name="agentId">The agent profile identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of active interactions for the agent.</returns>
    Task<int> CountActiveByAgentAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the most recent live interaction the specified agent is currently handling.
    /// </summary>
    /// <param name="agentId">The agent profile identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The active interaction, or <see langword="null"/> when the agent is not on a live interaction.</returns>
    Task<Interaction> FindActiveByAgentAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the most recent interactions handled by the specified agent, newest first.
    /// </summary>
    /// <param name="agentId">The agent profile identifier.</param>
    /// <param name="take">The maximum number of interactions to return.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The agent's most recent interactions.</returns>
    Task<IReadOnlyCollection<Interaction>> ListRecentByAgentAsync(string agentId, int take, CancellationToken cancellationToken = default);
}
