using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Defines the persistence contract for live agent sessions.
/// </summary>
public interface IAgentSessionStore : ICatalog<AgentSession>
{
    /// <summary>
    /// Finds the live session for the specified user.
    /// </summary>
    /// <param name="userId">The Orchard user identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching session, or <see langword="null"/> when none exists.</returns>
    Task<AgentSession> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists every online session whose heartbeat is older than the supplied cut-off time.
    /// </summary>
    /// <param name="heartbeatCutoffUtc">The UTC time before which a heartbeat is considered stale.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The stale online sessions.</returns>
    Task<IReadOnlyCollection<AgentSession>> ListStaleAsync(DateTime heartbeatCutoffUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists sessions for the specified users.
    /// </summary>
    /// <param name="userIds">The Orchard user identifiers.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matching agent sessions.</returns>
    Task<IReadOnlyCollection<AgentSession>> ListByUserIdsAsync(
        IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken = default);
}
