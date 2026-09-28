namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// Tracks which agents currently have the messaging workspace open. Routed (push) assignment needs to know an agent can
/// actually see a conversation the moment it lands, and the only evidence of that is the workspace itself
/// checking in; a voice session or a stored "available" flag says nothing about whether the inbox is on screen.
/// </summary>
public interface IMessagingPresenceTracker
{
    /// <summary>
    /// Records that the agent's workspace is open right now. Presence lapses on its own when the check-ins stop.
    /// </summary>
    /// <param name="agentId">The agent profile identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task TouchAsync(string agentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the agent's workspace has checked in recently enough to count as open.
    /// </summary>
    /// <param name="agentId">The agent profile identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when the agent is present.</returns>
    Task<bool> IsPresentAsync(string agentId, CancellationToken cancellationToken = default);
}
