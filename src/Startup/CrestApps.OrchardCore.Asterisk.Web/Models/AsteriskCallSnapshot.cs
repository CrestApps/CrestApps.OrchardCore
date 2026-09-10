namespace CrestApps.OrchardCore.Asterisk.Web.Models;

/// <summary>
/// Describes one logical phone call grouped from one or more underlying Asterisk channel legs.
/// </summary>
public sealed class AsteriskCallSnapshot
{
    /// <summary>
    /// Gets or sets the logical call key used to group related channel legs.
    /// </summary>
    public string Key { get; set; }

    /// <summary>
    /// Gets or sets the primary Asterisk channel identifier.
    /// </summary>
    public string PrimaryChannelId { get; set; }

    /// <summary>
    /// Gets or sets the caller display name for the grouped call.
    /// </summary>
    public string CallerName { get; set; }

    /// <summary>
    /// Gets or sets the caller number for the grouped call.
    /// </summary>
    public string CallerNumber { get; set; }

    /// <summary>
    /// Gets or sets the connected-party number for the grouped call.
    /// </summary>
    public string ConnectedNumber { get; set; }

    /// <summary>
    /// Gets or sets the inferred call direction.
    /// </summary>
    public string Direction { get; set; }

    /// <summary>
    /// Gets or sets the most relevant aggregated call state.
    /// </summary>
    public string State { get; set; }

    /// <summary>
    /// Gets or sets additional server-side detail behind the aggregated call state.
    /// </summary>
    public string StateDetail { get; set; }

    /// <summary>
    /// Gets or sets the ARI application name associated with the call.
    /// </summary>
    public string Application { get; set; }

    /// <summary>
    /// Gets or sets how many underlying Asterisk channel legs currently belong to this call.
    /// </summary>
    public int ChannelCount { get; set; }

    /// <summary>
    /// Gets or sets the estimated number of participating parties in the call.
    /// </summary>
    public int PartyCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether any call leg is on hold.
    /// </summary>
    public bool IsOnHold { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether any call leg is muted.
    /// </summary>
    public bool IsMuted { get; set; }

    /// <summary>
    /// Gets or sets the bridge identifier associated with the call, when any.
    /// </summary>
    public string BridgeId { get; set; }

    /// <summary>
    /// Gets or sets the bridge type associated with the call, when any.
    /// </summary>
    public string BridgeType { get; set; }

    /// <summary>
    /// Gets or sets when the earliest underlying channel was created.
    /// </summary>
    public DateTime? CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets how long the grouped call has been active, in seconds.
    /// </summary>
    public int DurationSeconds { get; set; }
}
