namespace CrestApps.OrchardCore.AI.Models;

/// <summary>
/// Represents the response from a text-to-speech synthesis operation.
/// </summary>
public sealed class TextToSpeechResponse
{
    /// <summary>
    /// Gets or sets the synthesized audio data.
    /// </summary>
    public byte[] AudioData { get; set; }

    /// <summary>
    /// Gets or sets the MIME content type of the audio (e.g., "audio/mp3", "audio/wav").
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the duration of the synthesized audio.
    /// </summary>
    public TimeSpan? Duration { get; set; }
}
