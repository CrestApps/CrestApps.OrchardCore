namespace CrestApps.OrchardCore.ContactCenter.Hubs;

/// <summary>
/// Describes a real-time change to an agent's presence broadcast to the agent's own connections and to
/// supervisor dashboards.
/// </summary>
public sealed class AgentPresenceNotification
{
    /// <summary>
    /// Gets or sets the identifier of the Orchard user the presence change applies to.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the agent profile.
    /// </summary>
    public string AgentId { get; set; }

    /// <summary>
    /// Gets or sets the display name shown for the agent.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the current presence status name.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the optional reason code associated with the current presence status.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the queues the agent is signed in to.
    /// </summary>
    public IList<string> QueueIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the UTC time the presence change occurred.
    /// </summary>
    public DateTime ChangedUtc { get; set; }
}
