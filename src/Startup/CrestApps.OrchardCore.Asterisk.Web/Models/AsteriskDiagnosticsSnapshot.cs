namespace CrestApps.OrchardCore.Asterisk.Web.Models;

/// <summary>
/// Captures a lightweight snapshot of the local Asterisk ARI diagnostics endpoints.
/// </summary>
public sealed class AsteriskDiagnosticsSnapshot
{
    /// <summary>
    /// Gets or sets when the snapshot was last refreshed.
    /// </summary>
    public DateTime LastUpdatedUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the ARI endpoint was reached successfully.
    /// </summary>
    public bool Reachable { get; set; }

    /// <summary>
    /// Gets or sets the diagnostics error message, when a refresh fails.
    /// </summary>
    public string ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the configured ARI base URL.
    /// </summary>
    public string BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the number of active ARI channels.
    /// </summary>
    public int ChannelCount { get; set; }

    /// <summary>
    /// Gets or sets the number of active ARI bridges.
    /// </summary>
    public int BridgeCount { get; set; }

    /// <summary>
    /// Gets or sets the estimated number of live calls.
    /// </summary>
    public int ActiveCallCount { get; set; }

    /// <summary>
    /// Gets or sets the number of connected channels.
    /// </summary>
    public int ConnectedChannelCount { get; set; }

    /// <summary>
    /// Gets or sets the number of ringing channels.
    /// </summary>
    public int RingingChannelCount { get; set; }

    /// <summary>
    /// Gets or sets the age of the oldest live channel, in seconds.
    /// </summary>
    public int OldestChannelSeconds { get; set; }

    /// <summary>
    /// Gets or sets the formatted ARI info payload.
    /// </summary>
    public string InfoJson { get; set; }

    /// <summary>
    /// Gets or sets the formatted ARI channels payload.
    /// </summary>
    public string ChannelsJson { get; set; }

    /// <summary>
    /// Gets or sets the formatted ARI bridges payload.
    /// </summary>
    public string BridgesJson { get; set; }

    /// <summary>
    /// Gets or sets the parsed active channel list.
    /// </summary>
    public IList<AsteriskChannelSnapshot> Channels { get; set; } = [];

    /// <summary>
    /// Gets or sets the parsed logical active call list grouped from the underlying channel legs.
    /// </summary>
    public IList<AsteriskCallSnapshot> Calls { get; set; } = [];

    /// <summary>
    /// Gets or sets the parsed active bridge list.
    /// </summary>
    public IList<AsteriskBridgeSnapshot> Bridges { get; set; } = [];
}
