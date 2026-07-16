namespace CrestApps.OrchardCore.Asterisk.Web.Models;

/// <summary>
/// Describes one active Asterisk bridge.
/// </summary>
public sealed class AsteriskBridgeSnapshot
{
    /// <summary>
    /// Gets or sets the bridge identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the bridge display name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the bridge type.
    /// </summary>
    public string BridgeType { get; set; }

    /// <summary>
    /// Gets or sets the number of channels currently joined to the bridge.
    /// </summary>
    public int ChannelCount { get; set; }

    /// <summary>
    /// Gets or sets the identifiers of the channels currently joined to the bridge.
    /// </summary>
    public IList<string> ChannelIds { get; set; } = [];

    /// <summary>
    /// Gets or sets when the bridge was created, when the server provides it.
    /// </summary>
    public DateTime? CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets how long the bridge has existed, in seconds.
    /// </summary>
    public int DurationSeconds { get; set; }
}
