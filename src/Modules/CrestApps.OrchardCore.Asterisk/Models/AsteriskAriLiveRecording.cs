using System.Text.Json.Serialization;

namespace CrestApps.OrchardCore.Asterisk.Models;

/// <summary>
/// Represents an in-progress Asterisk ARI live recording.
/// </summary>
internal sealed class AsteriskAriLiveRecording
{
    /// <summary>
    /// Gets or sets the recording name that uniquely addresses the live recording.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the recording lifecycle state (for example <c>recording</c> or <c>paused</c>).
    /// </summary>
    [JsonPropertyName("state")]
    public string State { get; set; }

    /// <summary>
    /// Gets or sets the media format the recording is captured in.
    /// </summary>
    [JsonPropertyName("format")]
    public string Format { get; set; }

    /// <summary>
    /// Gets or sets the elapsed duration of the recording, in seconds, when Asterisk reports it.
    /// </summary>
    [JsonPropertyName("duration")]
    public int? Duration { get; set; }
}
