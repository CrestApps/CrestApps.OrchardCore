using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the live, volatile real-time connection state of an agent. It is intentionally separate
/// from the administrator-owned <see cref="AgentProfile"/>: the profile holds durable configuration
/// (skills, capacity, queue membership) while the session tracks the agent's open SignalR connections,
/// heartbeat, and online status so routing can stop targeting an agent whose client has gone away.
/// </summary>
public sealed class AgentSession : CatalogItem, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the identifier of the Orchard user this session belongs to.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the user name of the Orchard user this session belongs to.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets the display name shown for the agent in supervisor and queue views.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the identifiers of the SignalR connections the agent currently has open.
    /// </summary>
    public IList<string> ConnectionIds { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the agent currently has at least one live connection.
    /// </summary>
    public bool IsOnline { get; set; }

    /// <summary>
    /// Gets or sets the queues the agent was signed in to when the session was last refreshed.
    /// </summary>
    public IList<string> QueueIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the dialer campaigns the agent was signed in to when the session was last refreshed.
    /// </summary>
    public IList<string> CampaignIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the UTC time the agent first connected for this session.
    /// </summary>
    public DateTime? ConnectedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the agent's client last sent a heartbeat.
    /// </summary>
    public DateTime? LastHeartbeatUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the agent's last connection dropped.
    /// </summary>
    public DateTime? LastDisconnectedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the session was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the session was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
