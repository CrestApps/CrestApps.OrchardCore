using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Asterisk.Models;

/// <summary>
/// Represents an Asterisk ARI channel.
/// </summary>
internal sealed class AsteriskAriChannel
{
    /// <summary>
    /// Gets or sets the ARI channel identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the ARI channel name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the ARI channel state.
    /// </summary>
    [JsonPropertyName("state")]
    public string State { get; set; }
}
