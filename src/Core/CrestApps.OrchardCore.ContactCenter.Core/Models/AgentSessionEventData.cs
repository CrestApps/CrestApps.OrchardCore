namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// An agent's connection to the platform starting, ending, or going silent: the payload of
/// <see cref="ContactCenterConstants.Events.AgentConnected"/>, <see cref="ContactCenterConstants.Events.AgentDisconnected"/>
/// and <see cref="ContactCenterConstants.Events.AgentHeartbeatLost"/>.
/// </summary>
public sealed class AgentSessionEventData
{
    /// <summary>
    /// Gets or sets the agent profile identifier.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the user the agent signs in as.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the agent session identifier.
    /// </summary>
    public string AgentSessionId { get; set; }

    /// <summary>
    /// Gets or sets the real-time connection identifier, when the event is about one connection.
    /// </summary>
    public string ConnectionId { get; set; }

    /// <summary>
    /// Gets or sets how many of the agent's connections are open after the event.
    /// </summary>
    public int OpenConnectionCount { get; set; }

    /// <summary>
    /// Gets or sets the last time the agent was heard from.
    /// </summary>
    public DateTime? LastHeartbeatUtc { get; set; }

    /// <summary>
    /// Gets or sets why the session ended, when it did.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets when the event happened, at full precision.
    /// </summary>
    public DateTime OccurredUtc { get; set; }
}
