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
    /// Records a heartbeat for the agent session so the cleanup task does not consider it stale. The stamp is
    /// committed in its own unit of work, so it is durable when this method returns rather than when the caller
    /// commits. A heartbeat that loses its version check to a concurrent writer is not recorded and does not
    /// throw; it is not retried, because a retry could write an older timestamp over a newer one.
    /// </summary>
    /// <param name="userId">The Orchard user identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The agent session as the caller's unit of work sees it, whether or not this heartbeat was the write that stamped it, or <see langword="null"/> when none exists.</returns>
    Task<AgentSession> HeartbeatAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the reconnect snapshot the agent desktop needs to restore its state.
    /// </summary>
    /// <param name="userId">The Orchard user identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The agent desktop snapshot.</returns>
    Task<AgentDesktopSnapshot> BuildSnapshotAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks sessions whose heartbeat has gone stale as offline and signs the agents out so routing no longer
    /// targets clients that are no longer connected. A single pass expires a bounded number of sessions, oldest
    /// heartbeat first, so a backlog left by an event that made every session stale at once — a deployment that
    /// drops every connection — is drained over consecutive passes rather than in one. A caller that needs the
    /// backlog cleared cannot assume one call is enough.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of sessions that were expired by this pass.</returns>
    Task<int> ExpireStaleAsync(CancellationToken cancellationToken = default);
}
