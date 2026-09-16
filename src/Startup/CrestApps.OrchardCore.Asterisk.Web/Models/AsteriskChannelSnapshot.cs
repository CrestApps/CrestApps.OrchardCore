namespace CrestApps.OrchardCore.Asterisk.Web.Models;

/// <summary>
/// Describes one active Asterisk channel.
/// </summary>
public sealed class AsteriskChannelSnapshot
{
    /// <summary>
    /// Gets or sets the channel identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the channel name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the current channel state.
    /// </summary>
    public string State { get; set; }

    /// <summary>
    /// Gets or sets the caller number.
    /// </summary>
    public string CallerNumber { get; set; }

    /// <summary>
    /// Gets or sets the caller display name.
    /// </summary>
    public string CallerName { get; set; }

    /// <summary>
    /// Gets or sets the connected-party number.
    /// </summary>
    public string ConnectedNumber { get; set; }

    /// <summary>
    /// Gets or sets the ARI application name.
    /// </summary>
    public string Application { get; set; }

    /// <summary>
    /// Gets or sets the inferred logical call key used to group related channel legs.
    /// </summary>
    public string LogicalCallKey { get; set; }

    /// <summary>
    /// Gets or sets the inferred leg direction.
    /// </summary>
    public string Direction { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider marked the channel as muted.
    /// </summary>
    public bool IsMuted { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the provider marked the channel as on hold.
    /// </summary>
    public bool IsOnHold { get; set; }

    /// <summary>
    /// Gets or sets the bridge identifier the channel currently belongs to, when any.
    /// </summary>
    public string BridgeId { get; set; }

    /// <summary>
    /// Gets or sets the bridge type the channel currently belongs to, when any.
    /// </summary>
    public string BridgeType { get; set; }

    /// <summary>
    /// Gets or sets when the channel was created, when the server provides it.
    /// </summary>
    public DateTime? CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets how long the channel has been active, in seconds.
    /// </summary>
    public int DurationSeconds { get; set; }
}
