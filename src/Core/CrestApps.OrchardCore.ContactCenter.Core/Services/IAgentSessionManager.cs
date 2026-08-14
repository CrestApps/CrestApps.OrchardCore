using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the management contract for live agent sessions.
/// </summary>
public interface IAgentSessionManager : ICatalogManager<AgentSession>
{
    /// <summary>
    /// Finds the live session for the specified user.
    /// </summary>
    /// <param name="userId">The Orchard user identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching session, or <see langword="null"/> when none exists.</returns>
    Task<AgentSession> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists online sessions whose heartbeat is older than the supplied cut-off time, oldest heartbeat first.
    /// The result is bounded, so a large backlog is drained across several calls rather than in one pass.
    /// </summary>
    /// <param name="heartbeatCutoffUtc">The UTC time before which a heartbeat is considered stale.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A bounded, oldest-heartbeat-first page of the stale online sessions.</returns>
    Task<IReadOnlyCollection<AgentSession>> GetStaleAsync(DateTime heartbeatCutoffUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists sessions for the specified users.
    /// </summary>
    /// <param name="userIds">The Orchard user identifiers.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching agent sessions.</returns>
    Task<IReadOnlyCollection<AgentSession>> GetByUserIdsAsync(
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken = default);
}
