using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Core.Indexes;

/// <summary>
/// Represents the YesSql index used to query live agent sessions by user and online status.
/// </summary>
public sealed class AgentSessionIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the user the session belongs to.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the agent currently has at least one live connection.
    /// </summary>
    public bool IsOnline { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the agent's client last sent a heartbeat.
    /// </summary>
    public DateTime? LastHeartbeatUtc { get; set; }
}
