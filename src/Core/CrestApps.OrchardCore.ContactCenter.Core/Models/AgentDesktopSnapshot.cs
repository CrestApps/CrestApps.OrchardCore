namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Represents the point-in-time state the agent desktop needs to render itself after an initial
/// connection or a reconnect. It combines the durable <see cref="AgentProfile"/> configuration with the
/// live <see cref="AgentSession"/> so a reconnecting client can restore presence, queue membership, and
/// any in-flight reservation without replaying the event history.
/// </summary>
public sealed class AgentDesktopSnapshot
{
    /// <summary>
    /// Gets or sets the identifier of the Orchard user the snapshot describes.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the display name shown for the agent.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an agent profile exists for the user.
    /// </summary>
    public bool HasProfile { get; set; }

    /// <summary>
    /// Gets or sets the current presence status name, or <see langword="null"/> when no profile exists.
    /// </summary>
    public string PresenceStatus { get; set; }

    /// <summary>
    /// Gets or sets the optional reason code associated with the current presence status.
    /// </summary>
    public string PresenceReason { get; set; }

    /// <summary>
    /// Gets or sets the pending presence status the system grants once in-flight routing completes.
    /// </summary>
    public string RequestedPresenceStatus { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the active reservation when the agent is reserved for an offer.
    /// </summary>
    public string ActiveReservationId { get; set; }

    /// <summary>
    /// Gets or sets the queues the agent is currently signed in to.
    /// </summary>
    public IList<string> QueueIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the dialer campaigns the agent is currently signed in to.
    /// </summary>
    public IList<string> CampaignIds { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the agent currently has at least one live connection.
    /// </summary>
    public bool IsOnline { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the agent's client last sent a heartbeat.
    /// </summary>
    public DateTime? LastHeartbeatUtc { get; set; }

    /// <summary>
    /// Gets or sets the authoritative server UTC time, used by the client to align local timers.
    /// </summary>
    public DateTime ServerTimeUtc { get; set; }
}
