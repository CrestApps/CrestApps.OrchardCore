using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Asterisk.Models;

/// <summary>
/// Represents an Asterisk ARI bridge.
/// </summary>
internal sealed class AsteriskAriBridge
{
    /// <summary>
    /// Gets or sets the ARI bridge identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the ARI bridge type.
    /// </summary>
    [JsonPropertyName("bridge_type")]
    public string BridgeType { get; set; }

    /// <summary>
    /// Gets or sets the channel identifiers currently in the bridge.
    /// </summary>
    [JsonPropertyName("channels")]
    public IList<string> Channels { get; set; } = [];
}
