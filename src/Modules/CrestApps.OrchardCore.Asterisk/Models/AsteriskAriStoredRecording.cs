using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Asterisk.Models;

/// <summary>
/// Represents an Asterisk ARI stored (completed) recording.
/// </summary>
internal sealed class AsteriskAriStoredRecording
{
    /// <summary>
    /// Gets or sets the recording name that uniquely addresses the stored recording.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the media format the recording is stored in.
    /// </summary>
    [JsonPropertyName("format")]
    public string Format { get; set; }

    /// <summary>
    /// Gets or sets the recording duration, in seconds, when the Asterisk build exposes it on the stored resource.
    /// The standard ARI stored-recording resource does not include a duration, so this remains <see langword="null"/>
    /// on most deployments.
    /// </summary>
    [JsonPropertyName("duration")]
    public int? Duration { get; set; }
}
