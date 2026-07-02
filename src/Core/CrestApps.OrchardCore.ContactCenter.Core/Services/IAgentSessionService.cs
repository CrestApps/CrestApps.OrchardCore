using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Coordinates the live connection lifecycle of agent sessions: registering and removing SignalR
/// connections, recording heartbeats, building reconnect snapshots, and expiring sessions whose client
/// has gone away so routing stops targeting a dead connection.
/// </summary>
public interface IAgentSessionService
{
    /// <summary>
    /// Registers a new live connection for the agent, creating the session when one does not exist and
    /// refreshing the queue and campaign membership snapshot from the agent profile.
    /// </summary>
    /// <param name="userId">The Orchard user identifier.</param>
    /// <param name="connectionId">The SignalR connection identifier.</param>
    /// <param name="userName">The user name of the agent.</param>
    /// <param name="displayName">The display name of the agent.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The agent session after the connection is registered.</returns>
    Task<AgentSession> ConnectAsync(string userId, string connectionId, string userName, string displayName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a live connection from the agent session and marks the session offline when no
    /// connections remain.
    /// </summary>
    /// <param name="userId">The Orchard user identifier.</param>
    /// <param name="connectionId">The SignalR connection identifier that dropped.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The agent session after the connection is removed, or <see langword="null"/> when none exists.</returns>
    Task<AgentSession> DisconnectAsync(string userId, string connectionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a heartbeat for the agent session so the cleanup task does not consider it stale.
    /// </summary>
    /// <param name="userId">The Orchard user identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The agent session after the heartbeat, or <see langword="null"/> when none exists.</returns>
    Task<AgentSession> HeartbeatAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the reconnect snapshot the agent desktop needs to restore its state.
    /// </summary>
    /// <param name="userId">The Orchard user identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The agent desktop snapshot.</returns>
    Task<AgentDesktopSnapshot> BuildSnapshotAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks every session whose heartbeat has gone stale as offline and signs the agent out so routing
    /// no longer targets a client that is no longer connected.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of sessions that were expired.</returns>
    Task<int> ExpireStaleAsync(CancellationToken cancellationToken = default);
}
